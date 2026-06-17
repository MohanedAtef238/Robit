import ctypes
import ctypes.wintypes
import time
import struct
import threading
import numpy as np
import cv2

from gazefollower.camera import Camera


class SharedMemoryCamera(Camera):
    """
    Reads camera frames from a Windows Memory Mapped File written by Unity's
    SharedCameraCapture component.

    Unity exclusively owns the physical webcam via WebCamTexture and publishes
    every frame into the MMF.  Any number of Python EXEs can instantiate this
    class and read frames independently without competing for the hardware handle.

    Binary header layout (40 bytes, little-endian):
        Offset  Size  Field
           0     4    MAGIC       = 0x524F4254
           4     4    VERSION     = 1
           8     4    Width
          12     4    Height
          16     4    FORMAT      = 1 (RGBA32)
          20     8    FrameId     (increments each new frame)
          28     8    Timestamp   (DateTime.UtcNow.Ticks)
          36     4    ActiveBuffer (0 or 1)

    Pixel data starts at offset 40.  The buffer alternates between two regions
    of (width * height * 4) bytes to prevent reading a partially-written frame.
    """

    MAGIC               = 0x524F4254
    VERSION             = 1
    PIXEL_FORMAT_RGBA32 = 1

    # Matches the C# WriteHeader() exactly.
    HEADER_STRUCT = struct.Struct("<I I I I I Q Q I")
    HEADER_SIZE   = HEADER_STRUCT.size          # == 40

    DEFAULT_MEMORY_NAME = "RobitCameraFrame"

    def __init__(self, memory_name=None, cam_fps=30):
        super().__init__()
        self.memory_name = memory_name or self.DEFAULT_MEMORY_NAME
        self.cam_fps     = cam_fps

        self._camera_thread_running = None
        self._camera_thread         = None

        self._kernel32 = ctypes.windll.kernel32
        self._configure_kernel32()
        self._handle = None
        self._addr   = None

        self._frame_id = -1

        self.width        = None
        self.height       = None
        self.frame_size   = None
        self._mapping_size = None

    # ── background capture thread ─────────────────────────────────────────────

    def _create_capture_thread(self):
        self._camera_thread_running = True
        self._camera_thread = threading.Thread(target=self.capture, daemon=True,
                                               name="SharedMemCam-reader")
        self._camera_thread.start()

    def capture(self):
        """
        Reads frames from the MMF on a background thread.
        Sleeps for one full frame period when no new frame is available so the
        thread does not busy-spin between Unity's ~33 ms Update() ticks.
        """
        # 250 checks per second (4ms sleep). Max added latency: 4ms, avg: 2ms.
        # Completely imperceptible for real-time tracking, half the CPU cost of 500/s.
        frame_sleep = 0.004

        while self._camera_thread_running:
            try:
                header = self._read(0, self.HEADER_SIZE)
                if len(header) < self.HEADER_SIZE:
                    time.sleep(frame_sleep)
                    continue

                try:
                    magic, version, w, h, fmt, frame_id, ts, active_buffer = \
                        self.HEADER_STRUCT.unpack(header)
                except struct.error:
                    time.sleep(frame_sleep)
                    continue

                if magic != self.MAGIC or fmt != self.PIXEL_FORMAT_RGBA32:
                    time.sleep(frame_sleep)
                    continue

                # No new frame yet — sleep a full frame period instead of 1 ms
                if frame_id == self._frame_id:
                    time.sleep(frame_sleep)
                    continue

                self._frame_id = frame_id

                offset = self.HEADER_SIZE + (active_buffer * w * h * 4)
                raw    = self._read(offset, w * h * 4)

                if len(raw) != w * h * 4:
                    time.sleep(frame_sleep)
                    continue

                arr   = np.frombuffer(raw, dtype=np.uint8)
                frame = arr.reshape((h, w, 4))

                # RGBA → RGB, vertical flip (Unity origin is bottom-left)
                frame = frame[:, :, :3]
                frame = cv2.flip(frame, 0)
                frame = np.ascontiguousarray(frame, dtype=np.uint8)

                timestamp = time.time_ns()

                with self.callback_and_param_lock:
                    if self.callback_func is not None:
                        self.callback_func(
                            self.camera_running_state,
                            timestamp,
                            frame,
                            *self.callback_args,
                            **self.callback_kwargs,
                        )

            except Exception:
                time.sleep(frame_sleep)

    # ── Camera interface ──────────────────────────────────────────────────────

    def open(self):
        """Opens the shared memory and starts the background reader thread."""
        print("[SharedMemoryCamera] Opening MMF connection.", flush=True)
        self._open_shared_memory()
        self._create_capture_thread()

    def close(self):
        """Unmaps the shared memory view and stops the reader thread."""
        print("[SharedMemoryCamera] Closing MMF connection.", flush=True)
        if self._camera_thread is not None:
            self._camera_thread_running = False
            self._camera_thread.join()
            self._camera_thread = None

        if self._addr:
            self._kernel32.UnmapViewOfFile(self._addr)
            self._addr = None

        if self._handle:
            self._kernel32.CloseHandle(self._handle)
            self._handle = None

    def set_on_image_callback(self, func, args=(), kwargs=None):
        super().set_on_image_callback(func, args, kwargs)

    def release(self):
        self._camera_thread_running = False
        if self._camera_thread is not None:
            self._camera_thread.join()
        self.close()

    # ── MMF helpers ───────────────────────────────────────────────────────────

    def _open_shared_memory(self, timeout=10.0):
        """
        Waits up to *timeout* seconds for Unity to create the MMF.
        Raises FileNotFoundError if it never appears.
        """
        self._configure_kernel32()

        deadline = time.time() + timeout
        handle   = None

        while time.time() < deadline:
            handle = self._kernel32.OpenFileMappingW(
                0x0004,   # FILE_MAP_READ
                False,
                self.memory_name,
            )
            if handle:
                break
            time.sleep(0.05)

        if not handle:
            raise FileNotFoundError(
                f"[SharedMemoryCamera] MMF '{self.memory_name}' not found after "
                f"{timeout}s.  Is SharedCameraCapture running in Unity?"
            )

        self._handle = handle
        self._addr   = self._kernel32.MapViewOfFile(handle, 0x0004, 0, 0, 0)

        if not self._addr:
            raise RuntimeError("[SharedMemoryCamera] MapViewOfFile failed.")

    def _configure_kernel32(self):
        self._kernel32.OpenFileMappingW.argtypes = [
            ctypes.wintypes.DWORD,
            ctypes.wintypes.BOOL,
            ctypes.wintypes.LPCWSTR,
        ]
        self._kernel32.OpenFileMappingW.restype  = ctypes.wintypes.HANDLE

        self._kernel32.MapViewOfFile.argtypes = [
            ctypes.wintypes.HANDLE,
            ctypes.wintypes.DWORD,
            ctypes.wintypes.DWORD,
            ctypes.wintypes.DWORD,
            ctypes.c_size_t,
        ]
        self._kernel32.MapViewOfFile.restype  = ctypes.c_void_p

        self._kernel32.UnmapViewOfFile.argtypes = [ctypes.c_void_p]
        self._kernel32.UnmapViewOfFile.restype  = ctypes.wintypes.BOOL

        self._kernel32.CloseHandle.argtypes = [ctypes.wintypes.HANDLE]
        self._kernel32.CloseHandle.restype  = ctypes.wintypes.BOOL

    def _read(self, offset, size):
        return ctypes.string_at(self._addr + offset, size)
