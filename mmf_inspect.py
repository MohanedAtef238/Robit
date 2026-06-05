import struct
import ctypes
import ctypes.wintypes
import time
import sys

MMF_NAME = 'RobitCameraFrame'
HEADER_SIZE = 40

k32 = ctypes.windll.kernel32

# ── Correct 64-bit-safe Win32 API declarations ────────────────────────────────
# MUST declare return types explicitly or ctypes truncates 64-bit pointers to 32-bit
k32.OpenFileMappingW.restype  = ctypes.wintypes.HANDLE
k32.OpenFileMappingW.argtypes = [ctypes.wintypes.DWORD, ctypes.wintypes.BOOL, ctypes.wintypes.LPCWSTR]

k32.MapViewOfFile.restype  = ctypes.c_void_p   # c_void_p preserves full 64-bit value
k32.MapViewOfFile.argtypes = [ctypes.wintypes.HANDLE, ctypes.wintypes.DWORD,
                               ctypes.wintypes.DWORD, ctypes.wintypes.DWORD, ctypes.c_size_t]

k32.UnmapViewOfFile.restype  = ctypes.wintypes.BOOL
k32.UnmapViewOfFile.argtypes = [ctypes.c_void_p]

k32.CloseHandle.restype  = ctypes.wintypes.BOOL
k32.CloseHandle.argtypes = [ctypes.wintypes.HANDLE]

FILE_MAP_READ = 0x0004

print(f"Opening MMF: {MMF_NAME}", flush=True)
h = k32.OpenFileMappingW(FILE_MAP_READ, False, MMF_NAME)
if not h:
    err = k32.GetLastError()
    print(f"FAIL: Could not open MMF. GetLastError={err}", flush=True)
    print("  -> Unity is NOT running or has not created the MMF.", flush=True)
    sys.exit(1)

print(f"MMF opened OK. Handle={h}", flush=True)

addr = k32.MapViewOfFile(h, FILE_MAP_READ, 0, 0, ctypes.c_size_t(HEADER_SIZE))
print(f"MapViewOfFile returned: {addr} (0x{addr & 0xFFFFFFFFFFFFFFFF:016X})", flush=True)

if addr is None or addr == 0:
    err = k32.GetLastError()
    print(f"FAIL: Could not map view. GetLastError={err}", flush=True)
    k32.CloseHandle(h)
    sys.exit(1)

print(f"\nReading header 5 times, 1 second apart:", flush=True)
print(f"{'#':<4} {'magic':<12} {'frameId':<12} {'width':<8} {'height':<8} {'activeBuf':<10} notes", flush=True)
print("-" * 70, flush=True)

prev_frame_id = None
for i in range(5):
    try:
        # addr is a Python int — use ctypes.string_at for safe 64-bit read
        data = ctypes.string_at(addr, HEADER_SIZE)

        magic      = struct.unpack_from('<I', data,  0)[0]
        version    = struct.unpack_from('<I', data,  4)[0]
        width      = struct.unpack_from('<I', data,  8)[0]
        height     = struct.unpack_from('<I', data, 12)[0]
        frame_id   = struct.unpack_from('<q', data, 20)[0]
        active_buf = struct.unpack_from('<I', data, 36)[0]

        delta = "" if prev_frame_id is None else f"(+{frame_id - prev_frame_id})"
        notes = ""
        if magic == 0:
            notes = "<-- ALL ZEROS: Unity created MMF but webcam never wrote"
        elif magic != 0x524F4254:
            notes = f"<-- WRONG MAGIC (expected 0x524F4254)"
        elif frame_id == 0:
            notes = "<-- frameId=0: webcam not yet writing frames"

        print(f"{i+1:<4} 0x{magic:08X}   {frame_id:<12} {width:<8} {height:<8} {active_buf:<10} {delta} {notes}", flush=True)
        prev_frame_id = frame_id
    except Exception as e:
        print(f"[{i+1}] ERROR reading memory: {e}", flush=True)

    if i < 4:
        time.sleep(1)

print("-" * 70, flush=True)
if prev_frame_id is None:
    print("DIAGNOSIS: Could not read any data from MMF.", flush=True)
elif prev_frame_id == 0 and magic == 0:
    print("DIAGNOSIS: MMF is all zeros -> webcam pipeline never started (culling bug).", flush=True)
elif prev_frame_id == 0:
    print("DIAGNOSIS: frameId stuck at 0 -> webcam opened but didUpdateThisFrame never true.", flush=True)
elif frame_id == struct.unpack_from('<q', ctypes.string_at(addr, HEADER_SIZE), 20)[0]:
    # same value as 5 seconds ago
    print("DIAGNOSIS: frameId NOT incrementing -> webcam is FROZEN. Culling bug still active.", flush=True)
else:
    print(f"DIAGNOSIS: frameId is incrementing -> Unity IS writing frames normally.", flush=True)
    print("  The bug is downstream: Python reader / calibration launch sequence.", flush=True)

k32.UnmapViewOfFile(addr)
k32.CloseHandle(h)
