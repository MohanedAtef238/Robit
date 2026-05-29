import argparse
import ctypes
import os
import socket
import sys
import time

import cv2
import pygame
from pygame.locals import KEYDOWN, K_ESCAPE




def build_config():
    from gazefollower.misc import CalibrationMode, DefaultConfig

    config = DefaultConfig()
    config.cali_mode = CalibrationMode.FIVE_POINT
    config.tilt_calibration = False
    config.tilt_calibration_vertical = False
    config.recalibration_normal = False
    config.split_calibration_background = True
    return config


def create_gaze_follower():
    from gazefollower import GazeFollower
    from gazefollower.camera import WebCamCamera

    return GazeFollower(camera=WebCamCamera(webcam_id=0), config=build_config())


def camera_check(port: int) -> None:
    """
    Opens the default webcam and communicates the result to Unity via UDP.

    Sends ``CAM_OK`` on success, then streams JPEG frames until the process is
    killed.  Sends ``CAM_ERROR:<reason>`` and exits immediately on failure.
    """
    UDP_DEST = ("127.0.0.1", port)
    JPEG_QUALITY = 60          # ~20-40 KB per frame at 640x480 — safe under 65 507-byte UDP limit
    FRAME_INTERVAL = 1.0 / 20  # 20 fps is plenty for a preview thumbnail

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)  # CAP_DSHOW avoids long open delays on Windows
        if not cap.isOpened():
            sock.sendto(b"CAM_ERROR:Could not open webcam (device 0 not found or in use)", UDP_DEST)
            return

        sock.sendto(b"CAM_OK", UDP_DEST)

        try:
            while True:
                ret, frame = cap.read()
                if not ret:
                    time.sleep(0.05)  # camera warming up
                    continue

                ok, buf = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, JPEG_QUALITY])
                if not ok:
                    continue

                data = buf.tobytes()
                if len(data) <= 65000:  # skip oversized frames rather than truncating
                    sock.sendto(data, UDP_DEST)

                time.sleep(FRAME_INTERVAL)
        finally:
            cap.release()
    finally:
        sock.close()


def probe_saved_calibration():
    gaze_follower = None
    try:
        gaze_follower = create_gaze_follower()
        print(f"HAS_SAVED_CALIBRATION:{1 if gaze_follower.calibration.has_calibrated else 0}", flush=True)
    finally:
        if gaze_follower is not None:
            gaze_follower.release()


def should_calibrate(mode, has_saved_calibration):
    if mode == "calibrate":
        return True
    if mode == "use-saved":
        return not has_saved_calibration
    return not has_saved_calibration


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=["auto", "calibrate", "use-saved", "camera-check"], default="auto")
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument("--status-only", action="store_true")
    args = parser.parse_args()

    if args.mode == "camera-check":
        camera_check(args.port)
        return

    sys.path.insert(0, os.getcwd())

    if args.status_only:
        probe_saved_calibration()
        return

    ctypes.windll.user32.SetProcessDPIAware()
    pygame.init()
    info = pygame.display.get_desktop_sizes()
    width, height = info[0]
    info = pygame.display.Info()
    win = pygame.display.set_mode((width,height), pygame.NOFRAME)
    print(f"{width}  x {height}",flush=True)
    pygame.display.set_caption("Gaze Calibration")

    gaze_follower = create_gaze_follower()
    # gaze_follower.preview(win=win) # Skip preview "tap to view" screen

    # Skip the "Press SPACE to continue" calibration instruction screen
    from gazefollower.ui import CalibrationUI
    def skip_guidance(self, instruction_text):
        self.running = False
    CalibrationUI.draw_guidance = skip_guidance

    has_saved_calibration = gaze_follower.calibration.has_calibrated
    if should_calibrate(args.mode, has_saved_calibration):
        gaze_follower.calibrate(win=win)
        gaze_follower.calibration.save_model()

    print("CALIBRATION_DONE", flush=True)

    try:
        pygame.display.iconify()
    except Exception:
        pass

    gaze_follower.start_sampling()
    pygame.time.wait(100)

    try:
        while True:
            for event in pygame.event.get():
                if event.type == KEYDOWN and event.key == K_ESCAPE:
                    return

            gaze_info = gaze_follower.get_gaze_info()
            if gaze_info and gaze_info.status:
                coords = gaze_info.filtered_gaze_coordinates
                if coords and len(coords) >= 2:
                    try:
                        x = float(coords[0])
                        y = float(coords[1])
                        print(f"GAZE:{x:.2f},{y:.2f}", flush=True)
                    except (TypeError, ValueError):
                        pass

            time.sleep(0.01)
    finally:
        pygame.time.wait(100)
        gaze_follower.stop_sampling()
        gaze_follower.release()
        pygame.quit()


if __name__ == "__main__":
    main()
