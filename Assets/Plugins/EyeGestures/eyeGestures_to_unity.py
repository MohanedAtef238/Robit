"""
EyeGestures → Unity UDP Bridge

Captures eye gaze via EyeGestures and sends coordinates to Unity over UDP.
Unity can send calibration commands back on a separate port.

Usage:
    python eyeGestures_to_unity.py              # With camera preview
    python eyeGestures_to_unity.py --headless   # No preview window
"""

import socket
import json
import argparse
import threading
import time
import ctypes

import numpy as np

import cv2
from eyeGestures import EyeGestures_v3
from eyeGestures.utils import VideoCapture

# ── Config ──
UDP_IP = "127.0.0.1"
SEND_PORT = 5005       # Gaze data → Unity
RECEIVE_PORT = 5006    # Commands ← Unity
HEARTBEAT_SEC = 1.0
UNITY_CONTEXT = "unity"


def get_screen_size():
    """Get primary monitor resolution via Windows API."""
    try:
        user32 = ctypes.windll.user32
        return user32.GetSystemMetrics(0), user32.GetSystemMetrics(1)
    except Exception:
        return 1920, 1080


def run(args):
    screen_w, screen_h = get_screen_size()
    print(f"[Bridge] Screen: {screen_w}x{screen_h}")

    # ── UDP sockets ──
    send_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    recv_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    recv_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    recv_sock.bind((UDP_IP, RECEIVE_PORT))
    recv_sock.settimeout(0.1)

    print(f"[Bridge] UDP send -> {UDP_IP}:{SEND_PORT}")
    print(f"[Bridge] UDP recv <- {UDP_IP}:{RECEIVE_PORT}")

    # ── EyeGestures ──
    gestures = EyeGestures_v3(calibration_radius=1000)
    gestures.addContext(UNITY_CONTEXT)
    gestures.setFixation(0.8)
    print("[Bridge] EyeGestures initialized")

    cap = VideoCapture(0)
    print("[Bridge] Camera opened")

    # ── State ──
    calibrating = False
    running = True
    last_data_time = 0
    calibration_points = {}

    def send_status(status: str, extra: dict | None = None):
        payload = {
            "type": "status",
            "status": status,
            "timestamp": time.time()
        }
        if extra:
            payload.update(extra)
        send(payload)

    def apply_calibration_map():
        if not calibration_points:
            send_status("CALIBRATION_SKIPPED", {"reason": "no_points"})
            return

        ordered_points = [calibration_points[idx] for idx in sorted(calibration_points.keys())]
        cal_array = np.array(ordered_points, dtype=np.float32)
        gestures.uploadCalibrationMap(cal_array, context=UNITY_CONTEXT)
        try:
            acceptance = max(40, int(min(screen_w, screen_h) * 0.05))
            gestures.clb[UNITY_CONTEXT].acceptance_radius = acceptance
        except Exception as e:
            print(f"[Bridge] Warning applying acceptance radius: {e}")
            acceptance = 0

        send_status("CALIBRATION_APPLIED", {
            "points": len(ordered_points),
            "acceptance_radius": acceptance
        })

    def send(data: dict):
        """Send JSON to Unity."""
        try:
            send_sock.sendto(json.dumps(data).encode(), (UDP_IP, SEND_PORT))
        except Exception as e:
            print(f"[Bridge] Send error: {e}")

    # ── Command listener (background thread) ──
    def listen_commands():
        nonlocal calibrating, running
        while running:
            try:
                data, _ = recv_sock.recvfrom(4096)
                msg = json.loads(data.decode())
                cmd = msg.get("command", "").upper()
                payload = msg.get("data")
                print(f"[Bridge] Command: {cmd}")

                if cmd == "CALIBRATE_START":
                    calibration_points.clear()
                    calibrating = True
                    send_status("CALIBRATION_STARTED", None)
                elif cmd == "CALIBRATE_POINT":
                    if isinstance(payload, dict):
                        idx = int(payload.get("index", len(calibration_points)))
                        px = float(payload.get("x", 0.5))
                        py = float(payload.get("y", 0.5))
                        calibration_points[idx] = (px, py)
                        send_status("CALIBRATION_POINT_RECEIVED", {
                            "received": len(calibration_points),
                            "last_index": idx
                        })
                elif cmd == "CALIBRATE_END":
                    calibrating = False
                    apply_calibration_map()
                elif cmd == "CALIBRATE_CANCEL":
                    calibrating = False
                    calibration_points.clear()
                    send_status("CALIBRATION_CANCELLED", None)
                elif cmd == "CHECK_CALIBRATION":
                    send_status("CALIBRATION_STATE", {
                        "points": len(calibration_points),
                        "calibrating": calibrating
                    })
                elif cmd == "QUIT":
                    running = False
            except socket.timeout:
                continue
            except Exception as e:
                if running:
                    print(f"[Bridge] Command error: {e}")

    cmd_thread = threading.Thread(target=listen_commands, daemon=True)
    cmd_thread.start()

    # ── Main loop ──
    print("[Bridge] Sending gaze data... (press 'q' to quit)")
    frame_count = 0

    try:
        while running:
            ret, frame = cap.read()
            if not ret:
                time.sleep(0.05)
                continue
            # this is where eye tracking does actually run
            try:
                # step() always returns (Gevent, Cevent) - > this is a future note
                event, _cevent = gestures.step(
                    frame,
                    calibration=calibrating,
                    width=screen_w,
                    height=screen_h,
                    context="unity"
                )

                # Send gaze data
                if event is not None and not calibrating:
                    # Check if point exists and has valid data
                    if hasattr(event, 'point') and len(event.point) >= 2:
                        gx, gy = event.point
                        send({
                            "type": "gaze",
                            "source": "eyegestures",
                            "gaze_x": float(gx),
                            "gaze_y": float(gy),
                            "norm_x": max(0.0, min(1.0, gx / screen_w)),
                            "norm_y": max(0.0, min(1.0, gy / screen_h)),
                            "blink": bool(event.blink) if hasattr(event, 'blink') else False,
                            "fixation": float(event.fixation) if hasattr(event, 'fixation') else 0.0,
                            "timestamp": time.time()
                        })
                        last_data_time = time.time()
                
            except AttributeError as e:
                # Catch internal EyeGestures/MediaPipe errors (e.g. NoneType has no attribute 'multi_face_landmarks')
                # only print every 60 frames to avoid spam
                if frame_count % 60 == 0:
                    print(f"[Bridge Warning] Tracking error (face lost?): {e}")
            except Exception as e:
                print(f"[Bridge Error] Unexpected error in step: {e}")

            # Heartbeat
            if time.time() - last_data_time > HEARTBEAT_SEC:
                # Send heartbeat so Unity knows the bridge is alive
                try:
                   send({"type": "heartbeat", "timestamp": time.time()})
                   last_data_time = time.time()
                except Exception:
                   pass

            # Preview window
            if not args.headless:
                if event is not None:
                    cv2.circle(frame, (int(event.point[0]), int(event.point[1])), 20, (0, 255, 0), 2)
                cv2.imshow("EyeGestures", frame)
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break

            frame_count += 1

    except KeyboardInterrupt:
        print("\n[Bridge] Interrupted")
    finally:
        print("[Bridge] Cleaning up...")
        running = False
        cap.close()
        if not args.headless:
            cv2.destroyAllWindows()
        send_sock.close()
        recv_sock.close()
        print("[Bridge] Stopped")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="EyeGestures → Unity Bridge")
    parser.add_argument("--headless", action="store_true", help="No preview window")
    run(parser.parse_args())
