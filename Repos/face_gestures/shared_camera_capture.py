import ctypes
import ctypes.wintypes
import struct
import time

import cv2
import numpy as np


class SharedMemoryCapture:
    MAGIC = 0x524F4254
    PIXEL_FORMAT_RGBA32 = 1
    HEADER_STRUCT = struct.Struct("<I I I I I Q Q I")
    HEADER_SIZE = HEADER_STRUCT.size

    def __init__(self, memory_name="RobitCameraFrame", timeout=5.0):
        self.memory_name = memory_name
        self._kernel32 = ctypes.windll.kernel32
        self._configure_kernel32()
        self._handle = None
        self._addr = None
        self._frame_id = -1
        self._frame_size = None
        self._open_shared_memory(timeout)

    def isOpened(self):
        return bool(self._addr)

    def read(self):
        if not self._addr:
            return False, None

        try:
            header = ctypes.string_at(self._addr, self.HEADER_SIZE)
            magic, _version, w, h, fmt, frame_id, _ts, active_buffer = self.HEADER_STRUCT.unpack(header)
            if magic != self.MAGIC or fmt != self.PIXEL_FORMAT_RGBA32:
                time.sleep(0.001)
                return False, None

            self._frame_size = w * h * 4
            if frame_id == self._frame_id:
                time.sleep(0.001)
                return False, None

            self._frame_id = frame_id
            offset = self.HEADER_SIZE + active_buffer * self._frame_size
            raw = ctypes.string_at(self._addr + offset, self._frame_size)
            rgba = np.frombuffer(raw, dtype=np.uint8).reshape((h, w, 4))
            bgr = cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGR)
            return True, bgr
        except Exception:
            time.sleep(0.001)
            return False, None

    def release(self):
        if self._addr:
            self._kernel32.UnmapViewOfFile(self._addr)
            self._addr = None
        if self._handle:
            self._kernel32.CloseHandle(self._handle)
            self._handle = None

    def _open_shared_memory(self, timeout):
        deadline = time.time() + timeout
        while time.time() < deadline:
            handle = self._kernel32.OpenFileMappingW(0x0004, False, self.memory_name)
            if handle:
                self._handle = handle
                break
            time.sleep(0.05)

        if not self._handle:
            raise FileNotFoundError(f"Shared memory '{self.memory_name}' not found")

        self._addr = self._kernel32.MapViewOfFile(self._handle, 0x0004, 0, 0, 0)
        if not self._addr:
            raise RuntimeError("MapViewOfFile failed")

    def _configure_kernel32(self):
        self._kernel32.OpenFileMappingW.argtypes = [
            ctypes.wintypes.DWORD,
            ctypes.wintypes.BOOL,
            ctypes.wintypes.LPCWSTR,
        ]
        self._kernel32.OpenFileMappingW.restype = ctypes.wintypes.HANDLE

        self._kernel32.MapViewOfFile.argtypes = [
            ctypes.wintypes.HANDLE,
            ctypes.wintypes.DWORD,
            ctypes.wintypes.DWORD,
            ctypes.wintypes.DWORD,
            ctypes.c_size_t,
        ]
        self._kernel32.MapViewOfFile.restype = ctypes.c_void_p

        self._kernel32.UnmapViewOfFile.argtypes = [ctypes.c_void_p]
        self._kernel32.UnmapViewOfFile.restype = ctypes.wintypes.BOOL

        self._kernel32.CloseHandle.argtypes = [ctypes.wintypes.HANDLE]
        self._kernel32.CloseHandle.restype = ctypes.wintypes.BOOL
