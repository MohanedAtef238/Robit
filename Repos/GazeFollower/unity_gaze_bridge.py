import os
import sys
import time
import socket
import argparse
import threading
import numpy as np

# Ensure pygame doesn't print welcome message to stdout
os.environ['PYGAME_HIDE_SUPPORT_PROMPT'] = "hide"

from gazefollower import GazeFollower
from gazefollower.misc import CameraRunningState
from SharedMemoryCamera import SharedMemoryCamera


def build_config():
    from gazefollower.misc import DefaultConfig
    config = DefaultConfig()
    # cali_mode is left at the default (THIRTEEN_POINT / 13) — it only affects
    # the legacy pygame calibration path.  The Unity-driven path
    # (run_unity_calibration) runs all four phases hard-coded:
    #   PHASE_LIGHT  (23 pts) + PHASE_DARK (23 pts)
    #   PHASE_RIGHT_TILT (7 pts) + PHASE_LEFT_TILT (7 pts)
    # = 60 calibration points / 2,700 training frames regardless of this value.
    config.tilt_calibration = True
    config.tilt_calibration_vertical = True
    config.recalibration_normal = False        # Unity drives the re-normal phase
    config.split_calibration_background = True # two-background phases active
    return config


def _get_models_dir(profile_id=None):
    """
    Returns (and creates) the directory where SVR calibration models are stored.
    
    We use the user's home directory (e.g., C:\\Users\\Username\\GazeFollower\\calibration)
    because if the built Unity application is installed in a restricted location
    like C:\\Program Files\\, the StreamingAssets folder will be read-only.
    """
    import pathlib
    models_dir = pathlib.Path.home().joinpath("GazeFollower", "calibration")
    if profile_id:
        models_dir = models_dir / profile_id
    models_dir.mkdir(parents=True, exist_ok=True)
    return models_dir


def create_gaze_follower(profile_id=None):
    from gazefollower import GazeFollower
    from gazefollower.camera import WebCamCamera
    from gazefollower.calibration import SVRCalibration
    config = build_config()

    # Explicitly create the directory before SVRCalibration is instantiated
    # so the 'else' branch inside SVRCalibration.__init__() is never given a
    # non-existent path (that branch does NOT call mkdir itself).
    models_dir = _get_models_dir(profile_id)
    calib = SVRCalibration(model_save_path=str(models_dir))

    # Validate the MMF handle WITHOUT starting the background reader thread.
    # Unity's GazeFollowerRunner already waits for SharedCameraCapture.IsReady
    # before spawning this EXE, so the 3-second timeout is conservative.
    camera = SharedMemoryCamera(
        memory_name="RobitCameraFrame",
        cam_fps=30,
    )
    try:
        camera._open_shared_memory(timeout=3.0)
        # Release the handle cleanly — GazeFollower will re-open it via open()
        camera._kernel32.UnmapViewOfFile(camera._addr)
        camera._kernel32.CloseHandle(camera._handle)
        camera._addr   = None
        camera._handle = None
        print("[unity_gaze_bridge] MMF validated. Using SharedMemoryCamera.", flush=True)
    except Exception as err:
        print(f"[unity_gaze_bridge] MMF unavailable ({err}). Falling back to WebCamCamera.", flush=True)
        camera = WebCamCamera(webcam_id=0)

    return GazeFollower(camera=camera, config=config, calibration=calib)


def probe_saved_calibration(profile_id=None):
    has_calibrated = check_has_saved_calibration(profile_id)
    print(f"HAS_SAVED_CALIBRATION:{1 if has_calibrated else 0}", flush=True)


def check_has_saved_calibration(profile_id=None):
    cali_dir = _get_models_dir(profile_id)
    return cali_dir.exists() and (
        len(list(cali_dir.glob("*.xml"))) > 0 or
        len(list(cali_dir.glob("*.bin"))) > 0
    )


# 23-point normal sequence derived from GazeFollower's cali_idx applied to
# generate_points() — 5x9 grid with 50px margins on a 1920x1080 screen.
# Indices (1-based): 23,1,3,5,7,9,10,12,16,18,19,21,25,27,28,30,34,36,37,39,41,45,23
CALIBRATION_POINTS_45 = [
    (0.500, 0.500),  # 23
    (0.026, 0.046),  # 1
    (0.145, 0.046),  # 2
    (0.263, 0.046),  # 3
    (0.382, 0.046),  # 4
    (0.500, 0.046),  # 5
    (0.618, 0.046),  # 6
    (0.737, 0.046),  # 7
    (0.855, 0.046),  # 8
    (0.974, 0.046),  # 9
    (0.026, 0.273),  # 10
    (0.145, 0.273),  # 11
    (0.263, 0.273),  # 12
    (0.382, 0.273),  # 13
    (0.500, 0.273),  # 14
    (0.618, 0.273),  # 15
    (0.737, 0.273),  # 16
    (0.855, 0.273),  # 17
    (0.974, 0.273),  # 18
    (0.026, 0.500),  # 19
    (0.145, 0.500),  # 20
    (0.263, 0.500),  # 21
    (0.382, 0.500),  # 22
    (0.618, 0.500),  # 24
    (0.737, 0.500),  # 25
    (0.855, 0.500),  # 26
    (0.974, 0.500),  # 27
    (0.026, 0.727),  # 28
    (0.145, 0.727),  # 29
    (0.263, 0.727),  # 30
    (0.382, 0.727),  # 31
    (0.500, 0.727),  # 32
    (0.618, 0.727),  # 33
    (0.737, 0.727),  # 34
    (0.855, 0.727),  # 35
    (0.974, 0.727),  # 36
    (0.026, 0.954),  # 37
    (0.145, 0.954),  # 38
    (0.263, 0.954),  # 39
    (0.382, 0.954),  # 40
    (0.500, 0.954),  # 41
    (0.618, 0.954),  # 42
    (0.737, 0.954),  # 43
    (0.855, 0.954),  # 44
    (0.974, 0.954),  # 45
    (0.500, 0.500),  # 23
]

FRAMES_PER_POINT = 23
PREPARE_TIME = 1.5  # seconds the user fixates before data collection starts


def run_unity_calibration(gf, udp_socket, target_port):
    """
    Unity-driven calibration mode. Unity renders the calibration dots;
    this function handles gaze data collection and SVR model fitting.
    Sends CALI_* UDP messages to Unity to drive the UI.
    No pygame window is created.

    PHASE SEQUENCE
    ==============
    Phase 1 — PHASE_LIGHT:      23 points on light background
    Phase 2 — PHASE_DARK:       23 points on dark background
    Phase 3 — PHASE_RIGHT_TILT: 7 right-edge points; preceded by a 4s
                                 tilt-prompt window where Unity shows the glow.
    Phase 4 — PHASE_LEFT_TILT:  7 left-edge points; same 4s prompt window.

    New UDP messages sent to Unity:
        CALI_PHASE <phase_id>      — signals a phase change
        CALI_START <total_points>  — total point count across all phases
    All other messages (CALI_SHOW_POINT, CALI_PROGRESS, CALI_POINT_DONE,
    CALI_MODEL_FITTING, CALI_MODEL_READY, CALI_MODEL_ERROR) are unchanged.
    """
    target_endpoint = ("127.0.0.1", target_port)

    def send(msg):
        if udp_socket and target_port > 0:
            udp_socket.sendto(msg.encode("utf-8"), target_endpoint)

    # Shared state between main thread and camera callback thread
    collecting_event = threading.Event()
    current_label = [None]   # [x_norm, y_norm] set by main thread
    frame_buffer = []        # list of (features, label) tuples
    frame_lock = threading.Lock()

    def on_calibration_frame(state, timestamp, frame):
        if state != CameraRunningState.CALIBRATING:
            return
        if not collecting_event.is_set():
            return
        label = current_label[0]
        if label is None:
            return

        face_info = gf.face_alignment.detect(timestamp, frame)
        gaze_info = gf.gaze_estimator.detect(frame, face_info)

        if gaze_info.status and gaze_info.features is not None:
            with frame_lock:
                if len(frame_buffer) < FRAMES_PER_POINT:
                    frame_buffer.append((gaze_info.features, label[:]))
                    count = len(frame_buffer)
                    progress = int(count * 100 / FRAMES_PER_POINT)
                    send(f"CALI_PROGRESS {progress}")

    gf.camera.set_on_image_callback(on_calibration_frame)

    # ── Phase definitions ────────────────────────────────────────────────────
    # Each entry: (phase_id, points_list, tilt_pause_seconds)
    # tilt_pause gives Unity time to show the glow animation before dots start.
    TILT_PAUSE = 4.0
    phases = [
        ("PHASE_LIGHT",      CALIBRATION_POINTS_45,         0.0),
    ]

    total_points = sum(len(pts) for _, pts, _ in phases)
    send(f"CALI_START {total_points}")

    all_features = []
    all_labels = []
    global_idx = 0

    try:
        gf.camera.start_calibrating()

        for phase_id, calibration_points, tilt_pause in phases:
            # Notify Unity which phase we are entering
            send(f"CALI_PHASE {phase_id}")

            # For tilt phases, wait so Unity can display the glow prompt
            if tilt_pause > 0:
                time.sleep(tilt_pause)

            for x_norm, y_norm in calibration_points:
                global_idx += 1
                send(f"CALI_SHOW_POINT {x_norm:.4f},{y_norm:.4f} {global_idx} {total_points}")

                # Wait for the user to fixate on the dot
                time.sleep(PREPARE_TIME)

                # Start collecting frames for this point
                with frame_lock:
                    frame_buffer.clear()
                current_label[0] = [x_norm, y_norm]
                collecting_event.set()

                # ── Pause/Resume collection loop ──────────────────────────
                # Instead of a hard timeout that aborts calibration, we detect
                # camera stalls (no new frames for STALL_TIMEOUT seconds) and
                # enter a paused state.  Collection resumes automatically when
                # the camera comes back, preserving all data collected so far.
                STALL_TIMEOUT = 5.0   # seconds of silence before declaring paused
                is_paused = False
                last_count = 0
                stall_start = time.time()

                while True:
                    with frame_lock:
                        count = len(frame_buffer)

                    if count >= FRAMES_PER_POINT:
                        # All frames for this point collected — continue
                        if is_paused:
                            send("CALI_RESUMED")
                        break

                    if count > last_count:
                        # New frames are arriving — reset stall timer
                        last_count = count
                        stall_start = time.time()
                        if is_paused:
                            # Camera has recovered
                            is_paused = False
                            send("CALI_RESUMED")
                    elif not is_paused and (time.time() - stall_start) > STALL_TIMEOUT:
                        # No new frames for STALL_TIMEOUT seconds — camera stalled
                        is_paused = True
                        collecting_event.clear()
                        send("CALI_PAUSED")

                    if is_paused:
                        # The stall is in Unity's WebCamTexture, not the MMF handle.
                        # Closing/reopening the MMF has no effect on a Unity-side driver stall.
                        # Back off, re-arm the callback, and wait for frames to resume naturally.
                        time.sleep(0.5)
                        collecting_event.set()   # re-arm so the callback fires when frames return
                        stall_start = time.time()  # reset so we don't re-send CALI_PAUSED immediately
                    else:
                        time.sleep(0.05)

                collecting_event.clear()
                current_label[0] = None

                with frame_lock:
                    for feats, lbl in frame_buffer[:FRAMES_PER_POINT]:
                        all_features.append(feats)
                        all_labels.append(lbl)

                send(f"CALI_POINT_DONE {global_idx} {total_points}")
                time.sleep(0.3)  # brief pause before next point


    finally:
        gf.camera.stop_calibrating()

    if len(all_features) < 10:
        send("CALI_MODEL_ERROR not enough data collected for SVR fitting")
        return

    send("CALI_MODEL_FITTING")

    try:
        features_arr = np.array(all_features, dtype=np.float32)
        labels_arr = np.array(all_labels, dtype=np.float32)
        has_calibrated, mean_error, _ = gf.calibration.calibrate(features_arr, labels_arr)
        if has_calibrated:
            gf.calibration.save_model()
            send(f"CALI_MODEL_READY {mean_error:.4f}")
        else:
            send("CALI_MODEL_ERROR SVR training failed — insufficient variation in gaze data")
    except Exception as ex:
        send(f"CALI_MODEL_ERROR {ex}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=0, help="UDP port to send data to")
    parser.add_argument("--mode", type=str,
                        choices=["auto", "calibrate", "use-saved", "unity-calibrate"],
                        default="auto")
    parser.add_argument("--status-only", action="store_true")
    parser.add_argument("--keyboard", action="store_true", help="Enable arrow key simulation")
    parser.add_argument("--profile-id", type=str, default=None, help="Profile ID to load calibration for")
    args = parser.parse_args()

    sys.path.insert(0, os.getcwd())

    if args.status_only:
        probe_saved_calibration(args.profile_id)
        sys.exit(0)

    target_port = args.port
    print(f"Starting Gaze bridge, mode: {args.mode}, UDP port: {target_port}, Profile: {args.profile_id}")

    udp_socket = None
    if target_port > 0:
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    # ── Unity-driven calibration (no pygame window) ───────────────────────────
    if args.mode == "unity-calibrate":
        gf = create_gaze_follower(args.profile_id)
        try:
            run_unity_calibration(gf, udp_socket, target_port)
        finally:
            gf.release()
            if udp_socket:
                udp_socket.close()
        return

    # ── Headless Tracking Mode or Pygame Mode ─────────────────────────────────
    gf = create_gaze_follower(args.profile_id)
    has_saved_calibration = gf.calibration.has_calibrated

    # If keyboard simulation is enabled, fallback to Pygame since it provides key events.
    if args.keyboard:
        import pygame
        pygame.init()
        win = pygame.display.set_mode((1920, 1080), pygame.FULLSCREEN)
        
        if args.mode == "calibrate" or (args.mode == "auto" and not has_saved_calibration):
            gf.preview(win=win)
            gf.calibrate(win=win)
            gf.calibration.save_model()
            print("CALIBRATION_DONE", flush=True)
        else:
            print("CALIBRATION_DONE", flush=True)

        gf.start_sampling()
        pygame.time.wait(100)

        gx, gy = 960.0, 540.0
        try:
            while True:
                pygame.event.pump()
                keys = pygame.key.get_pressed()
                speed = 10.0
                if keys[pygame.K_LEFT]:  gx -= speed
                if keys[pygame.K_RIGHT]: gx += speed
                if keys[pygame.K_UP]:    gy -= speed
                if keys[pygame.K_DOWN]:  gy += speed

                gx = max(0.0, min(1920.0, gx))
                gy = max(0.0, min(1080.0, gy))

                if udp_socket:
                    udp_socket.sendto(f"{gx},{gy}".encode("utf-8"), ("127.0.0.1", target_port))

                pygame.time.wait(16)
        except KeyboardInterrupt:
            pass
        finally:
            gf.stop_sampling()
            gf.release()
            pygame.quit()
            if udp_socket:
                udp_socket.close()
        return

    # ── Headless Tracking Mode (No Pygame, No GUI) ────────────────────────────
    if args.mode == "calibrate" or (args.mode == "auto" and not has_saved_calibration):
        print("ERROR: Calibration required but running in headless mode without Unity calibration. Use unity-calibrate mode.")
        return

    print("CALIBRATION_DONE", flush=True)
    gf.start_sampling()
    time.sleep(0.1)

    try:
        while True:
            gaze_info = gf.get_gaze_info()
            if gaze_info and gaze_info.status:
                coords = gaze_info.filtered_gaze_coordinates
                if coords and len(coords) >= 2:
                    try:
                        x = float(coords[0])
                        y = float(coords[1])
                        print(f"GAZE:{x:.2f},{y:.2f}", flush=True)
                        if udp_socket:
                            udp_socket.sendto(f"{x},{y}".encode("utf-8"), ("127.0.0.1", target_port))
                    except (TypeError, ValueError):
                        pass
            time.sleep(0.016)
    except KeyboardInterrupt:
        pass
    finally:
        gf.stop_sampling()
        gf.release()
        if udp_socket:
            udp_socket.close()


if __name__ == "__main__":
    main()

