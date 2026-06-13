import sys
import os

import cv2

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from face_gestures import FaceGestureDetector
from eye_movement import GazeCalibrator, GazeResult, get_gaze_offset


def setup_camera(camera_index: int = 0) -> cv2.VideoCapture:
    cap = cv2.VideoCapture(camera_index)
    if not cap.isOpened():
        raise RuntimeError(f"Cannot open webcam at index {camera_index}")
    return cap


def handle_user_input(detector: FaceGestureDetector) -> bool:
    """Returns False if the user pressed 'q', True otherwise."""
    key = cv2.waitKey(1) & 0xFF
    if key == ord("q"):
        return False
    if key == ord(" "):
        detector.start_calibration()
    return True


def feed_gaze_calibrator(
    detector: FaceGestureDetector,
    gaze_cal: GazeCalibrator,
) -> bool:
    """Feed the current frame's gaze offset to the calibrator.
    Returns True when calibration completes for the first time."""
    offset = get_gaze_offset(detector._last_lm)
    just_finished = gaze_cal.feed(offset)
    if just_finished and len(gaze_cal._samples) == gaze_cal._calib_frames:
        print(f"Gaze calibration done — baseline offset: {gaze_cal._baseline:.4f}")
    return just_finished


def get_gaze_result(
    detector: FaceGestureDetector,
    gaze_cal: GazeCalibrator,
) -> GazeResult:
    offset = get_gaze_offset(detector._last_lm)
    return gaze_cal.classify(offset)


def should_report(result: GazeResult, last: GazeResult) -> bool:
    if result.direction != last.direction:
        return True
    return result.direction != "straight" and abs(result.speed - last.speed) >= 2


def process_frame(
    detector: FaceGestureDetector,
    gaze_cal: GazeCalibrator,
    active: bool,
    last_result: GazeResult,
) -> tuple[bool, GazeResult]:
    """Run one frame of gaze logic. Returns (active, last_result)."""
    if detector.is_calibrating():
        feed_gaze_calibrator(detector, gaze_cal)
        return active, last_result

    if not gaze_cal.is_calibrated:
        feed_gaze_calibrator(detector, gaze_cal)
        return active, last_result

    if detector.triple_blinked():
        active = not active
        state = "STARTED" if active else "STOPPED"
        print(f"Triple blink detected! Eye scroll mode {state}.")
        last_result = GazeResult("straight", 0)

    if active:
        result = get_gaze_result(detector, gaze_cal)
        if should_report(result, last_result):
            print(f"Eye gaze: {result.direction.upper():<8} | Speed: {result.speed}")
            last_result = result

    return active, last_result


def main() -> None:
    cap = setup_camera()
    detector = FaceGestureDetector()
    gaze_cal = GazeCalibrator()

    print("Press SPACE to calibrate. Press 'q' to quit.")

    active      = False
    last_result = GazeResult("straight", 0)

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                break

            frame = cv2.flip(frame, 1)
            if detector.process(frame):
                active, last_result = process_frame(
                    detector, gaze_cal, active, last_result
                )

            detector.draw_overlay(frame)
            cv2.imshow("Eye Gaze Scroll", frame)

            if not handle_user_input(detector):
                break
    finally:
        cap.release()
        cv2.destroyAllWindows()
        detector.close()


if __name__ == "__main__":
    main()
