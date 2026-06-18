import time

import cv2
from face_gestures import FaceGestureDetector
from shared_camera_capture import SharedMemoryCapture


def open_capture():
    try:
        cap = SharedMemoryCapture(memory_name="RobitCameraFrame", timeout=5.0)
        print("Using Unity shared camera: RobitCameraFrame", flush=True)
        return cap
    except Exception as err:
        print(f"Shared camera unavailable ({err}); falling back to webcam.", flush=True)

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        raise RuntimeError("Cannot open webcam")
    return cap


def main():
    cap = open_capture()
    detector = FaceGestureDetector()
    is_calibrated = False

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                time.sleep(0.001)
                continue

            if isinstance(cap,SharedMemoryCapture):
                frame = cv2.flip(frame, 0)

            face_found = detector.process(frame)

            if face_found and not detector.is_calibrating():
                if detector.eyebrow_raised():
                    print("raise_eyebrow",flush=True)
                if detector.triple_blinked():
                    print("triple blink detected",flush=True)

            # detector.draw_overlay(frame)
            # cv2.imshow("Face Gestures", frame)

            key = cv2.waitKey(1) & 0xFF
            if key == ord("q"):
                break
            if key == ord(" ") or not is_calibrated: 
                detector.start_calibration()
    finally:
        cap.release()
        cv2.destroyAllWindows()
        detector.close()


if __name__ == "__main__":
    main()
