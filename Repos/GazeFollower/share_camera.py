"""
share_camera.py — Robit Shared Camera Writer
=============================================
Owns the webcam exclusively via OpenCV.
Writes raw RGBA frames to the "RobitCameraFrame" named MMF.

WHY THIS EXISTS:
  Unity's WebCamTexture on Windows requires the texture to be rendered
  every frame to stay alive. This creates a fragile dependency on Unity's
  rendering pipeline. When any rendering hiccup occurs (scene transitions,
  URP culling, etc.), the webcam freezes and Python's gaze bridge falls
  back to grabbing the webcam directly — which conflicts with Unity.

  By moving webcam ownership here, neither Unity nor Python need to touch
  the hardware. Both just read from shared memory, which is trivially cheap.

PIXEL ORDER — CRITICAL:
  Unity's Texture2D.SetPixels32() and the original WebCamTexture.GetPixels32()
  both use BOTTOM-TO-TOP row order. The compiled gaze bridge expects this.
  OpenCV delivers frames TOP-TO-BOTTOM, so we flip vertically before writing.

MMF FORMAT (same as SharedCameraCapture.cs):
  Offset  Size  Field
  0       4     MAGIC (0x524F4254 = "ROBT")
  4       4     VERSION (1)
  8       4     width
  12      4     height
  16      4     format (1 = RGBA32)
  20      8     frameId (int64, little-endian)
  28      8     timestamp (Windows FILETIME ticks)
  36      4     activeBuffer (0 or 1)
  40      ...   pixel data (double-buffered)

Usage: share_camera.exe [--device 0] [--width 1280] [--height 720] [--fps 30]
"""

import struct
import time
import ctypes
import ctypes.wintypes
import argparse
import sys
import cv2
import numpy as np

# ── MMF constants (must match SharedCameraCapture.cs) ─────────────────────────
MEMORY_NAME   = "RobitCameraFrame"
MAGIC         = 0x524F4254
VERSION       = 1
FORMAT_RGBA32 = 1
HEADER_SIZE   = 40
MAX_W, MAX_H  = 1920, 1080

# Windows FILETIME epoch offset (ticks between 0001-01-01 and 1970-01-01)
FILETIME_EPOCH_OFFSET = 116444736000000000

# ── Win32 API for named shared memory ─────────────────────────────────────────
k32 = ctypes.windll.kernel32

k32.CreateFileMappingW.restype  = ctypes.wintypes.HANDLE
k32.CreateFileMappingW.argtypes = [
    ctypes.wintypes.HANDLE,
    ctypes.c_void_p,
    ctypes.wintypes.DWORD,
    ctypes.wintypes.DWORD,
    ctypes.wintypes.DWORD,
    ctypes.wintypes.LPCWSTR,
]

k32.MapViewOfFile.restype  = ctypes.c_void_p
k32.MapViewOfFile.argtypes = [
    ctypes.wintypes.HANDLE, ctypes.wintypes.DWORD,
    ctypes.wintypes.DWORD, ctypes.wintypes.DWORD, ctypes.c_size_t,
]

k32.UnmapViewOfFile.restype  = ctypes.wintypes.BOOL
k32.UnmapViewOfFile.argtypes = [ctypes.c_void_p]

k32.CloseHandle.restype  = ctypes.wintypes.BOOL
k32.CloseHandle.argtypes = [ctypes.wintypes.HANDLE]

PAGE_READWRITE       = 0x04
FILE_MAP_ALL_ACCESS  = 0xF001F
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value


def windows_ticks():
    return int(time.time() * 10_000_000) + FILETIME_EPOCH_OFFSET


def create_mmf(name: str, size: int):
    size_hi = (size >> 32) & 0xFFFFFFFF
    size_lo = size & 0xFFFFFFFF
    h = k32.CreateFileMappingW(INVALID_HANDLE_VALUE, None, PAGE_READWRITE, size_hi, size_lo, name)
    if not h:
        raise RuntimeError(f"CreateFileMappingW failed: {k32.GetLastError()}")
    addr = k32.MapViewOfFile(h, FILE_MAP_ALL_ACCESS, 0, 0, ctypes.c_size_t(size))
    if not addr:
        k32.CloseHandle(h)
        raise RuntimeError(f"MapViewOfFile failed: {k32.GetLastError()}")
    return h, addr


def write_header(addr, frame_id: int, width: int, height: int, active_buf: int):
    header = struct.pack('<IIIIIqqi',
        MAGIC, VERSION, width, height, FORMAT_RGBA32,
        frame_id, windows_ticks(), active_buf,
    )
    ctypes.memmove(addr, header, HEADER_SIZE)


def open_camera(device: int, width: int, height: int, fps: int):
    """
    Try MSMF first (better sustained throughput on Windows 11 at HD resolutions),
    fall back to DirectShow.
    """
    for backend, name in [(cv2.CAP_MSMF, "MSMF"), (cv2.CAP_DSHOW, "DirectShow")]:
        cap = cv2.VideoCapture(device, backend)
        if not cap.isOpened():
            continue
        cap.set(cv2.CAP_PROP_FRAME_WIDTH,  width)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
        cap.set(cv2.CAP_PROP_FPS,          fps)
        cap.set(cv2.CAP_PROP_BUFFERSIZE,   1)
        actual_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        actual_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        print(f"[SCC-Writer] Opened device {device} via {name} at {actual_w}x{actual_h}", flush=True)
        return cap, actual_w, actual_h
    return None, 0, 0


def main():
    parser = argparse.ArgumentParser(description="Robit Shared Camera Writer")
    parser.add_argument("--device", type=int, default=0,    help="Webcam device index")
    parser.add_argument("--width",  type=int, default=1280, help="Requested width")
    parser.add_argument("--height", type=int, default=720,  help="Requested height")
    parser.add_argument("--fps",    type=int, default=30,   help="Requested FPS")
    args = parser.parse_args()

    cap, actual_w, actual_h = open_camera(args.device, args.width, args.height, args.fps)
    if cap is None:
        print(f"[SCC-Writer] ERROR: Could not open webcam device {args.device}", file=sys.stderr, flush=True)
        sys.exit(1)

    # Pre-allocate RGBA output buffer (avoids per-frame heap allocation)
    frame_rgba = np.empty((actual_h, actual_w, 4), dtype=np.uint8)

    max_frame_bytes = MAX_W * MAX_H * 4
    total_size      = HEADER_SIZE + max_frame_bytes * 2
    h_mmf, addr     = create_mmf(MEMORY_NAME, total_size)
    print(f"[SCC-Writer] MMF '{MEMORY_NAME}' created ({total_size // 1024} KB)", flush=True)

    frame_id   = 0
    active_buf = 0
    fail_count = 0
    MAX_FAILURES = 30

    try:
        while True:
            ret, frame_bgr = cap.read()

            if not ret:
                fail_count += 1
                if fail_count >= MAX_FAILURES:
                    print("[SCC-Writer] Restarting webcam after too many failures...", flush=True)
                    cap.release()
                    time.sleep(0.5)
                    cap, actual_w, actual_h = open_camera(args.device, args.width, args.height, args.fps)
                    if cap is None:
                        print("[SCC-Writer] ERROR: Could not reopen webcam", file=sys.stderr, flush=True)
                        break
                    frame_rgba = np.empty((actual_h, actual_w, 4), dtype=np.uint8)
                    fail_count = 0
                # Bump frameId even on failure so readers don't timeout
                frame_id += 1
                write_header(addr, frame_id, actual_w, actual_h, active_buf)
                time.sleep(0.01)
                continue

            fail_count = 0
            h, w = frame_bgr.shape[:2]

            # ── Flip vertically to match Unity/gaze-bridge expected row order ──
            # Unity's Texture2D.SetPixels32() and WebCamTexture.GetPixels32()
            # use bottom-to-top row order. OpenCV delivers top-to-bottom.
            # Flipping here means both the preview and the gaze bridge get
            # correctly oriented frames — face detection requires upright faces.
            frame_bgr_flipped = cv2.flip(frame_bgr, 0)

            # Convert BGR → RGBA into pre-allocated buffer (no heap alloc)
            cv2.cvtColor(frame_bgr_flipped, cv2.COLOR_BGR2RGBA, dst=frame_rgba[:h, :w])

            # Write directly from numpy buffer pointer (no tobytes() copy)
            frame_contiguous = np.ascontiguousarray(frame_rgba[:h, :w])
            active_buf  = 1 - active_buf
            frame_bytes = w * h * 4
            offset      = HEADER_SIZE + active_buf * frame_bytes
            ctypes.memmove(addr + offset, frame_contiguous.ctypes.data, frame_bytes)

            frame_id += 1
            write_header(addr, frame_id, w, h, active_buf)

            if frame_id % 150 == 1:
                print(f"[SCC-Writer] frameId={frame_id}, res={w}x{h}", flush=True)

    except KeyboardInterrupt:
        print("[SCC-Writer] Interrupted.", flush=True)
    finally:
        cap.release()
        k32.UnmapViewOfFile(ctypes.c_void_p(addr))
        k32.CloseHandle(h_mmf)
        print("[SCC-Writer] Webcam and MMF released.")


if __name__ == "__main__":
    main()
