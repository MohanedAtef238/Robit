import cv2
from face_gestures import FaceGestureDetector


def main():
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        raise RuntimeError("Cannot open webcam")

    detector = FaceGestureDetector()

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                break

            frame = cv2.flip(frame, 1)
            face_found = detector.process(frame)

            if face_found and not detector.is_calibrating():
                if detector.eyebrow_raised():
                    print("eyebrows raised")
                if detector.triple_blinked():
                    print("triple blink detected")

            detector.draw_overlay(frame)
            cv2.imshow("Face Gestures", frame)

            key = cv2.waitKey(1) & 0xFF
            if key == ord("q"):
                break
            if key == ord(" "):
                detector.start_calibration()
    finally:
        cap.release()
        cv2.destroyAllWindows()
        detector.close()


if __name__ == "__main__":
    main()
