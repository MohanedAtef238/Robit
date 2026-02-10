"""
Standalone EyeGestures v3 Test
No UDP, no Unity -- just camera + EyeGestures + OpenCV.
Run with: .venv/Scripts/python.exe test_eyegestures.py
"""

import ctypes
import time
import cv2
from eyeGestures import EyeGestures_v3
from eyeGestures.utils import VideoCapture


def get_screen_size():
    try:
        user32 = ctypes.windll.user32
        return user32.GetSystemMetrics(0), user32.GetSystemMetrics(1)
    except Exception:
        return 1920, 1080


def main():
    screen_w, screen_h = get_screen_size()
    print(f"[Test] Screen size: {screen_w}x{screen_h}")

    # Initialize exactly like the official README example
    gestures = EyeGestures_v3()
    cap = VideoCapture(0)
    print("[Test] Camera opened, starting tracking loop...")
    print("[Test] Press 'q' to quit.")

    calibrate = True
    context = "test"
    frame_count = 0
    gaze_count = 0
    last_print = time.time()

    while True:
        ret, frame = cap.read()
        if not ret:
            time.sleep(0.05)
            continue

        event, cevent = gestures.step(
            frame,
            calibration=calibrate,
            width=screen_w,
            height=screen_h,
            context=context
        )

        frame_count += 1

        if event is not None:
            gaze_count += 1
            gx, gy = event.point[0], event.point[1]
            blink = event.blink
            fixation = event.fixation

            # Draw gaze point on frame
            draw_x = int(gx * frame.shape[1] / screen_w)
            draw_y = int(gy * frame.shape[0] / screen_h)
            draw_x = max(0, min(draw_x, frame.shape[1] - 1))
            draw_y = max(0, min(draw_y, frame.shape[0] - 1))

            color = (0, 0, 255) if blink else (0, 255, 0)
            cv2.circle(frame, (draw_x, draw_y), 15, color, 2)
            cv2.putText(frame, f"Gaze: ({gx:.0f}, {gy:.0f})", (10, 30),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
            cv2.putText(frame, f"Blink: {blink}  Fix: {fixation:.2f}", (10, 60),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
        else:
            cv2.putText(frame, "No face detected", (10, 30),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)

        # Print stats every 2 seconds
        now = time.time()
        if now - last_print > 2.0:
            fps = frame_count / (now - last_print)
            print(f"[Test] frames={frame_count}  gaze_events={gaze_count}  fps={fps:.1f}")
            frame_count = 0
            gaze_count = 0
            last_print = now

        cv2.putText(frame, f"Gaze events: {gaze_count}", (10, frame.shape[0] - 20),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 1)

        cv2.imshow("EyeGestures Standalone Test", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    cap.close()
    cv2.destroyAllWindows()
    print("[Test] Done.")


if __name__ == "__main__":
    main()
