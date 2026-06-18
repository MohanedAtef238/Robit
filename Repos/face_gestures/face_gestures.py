import os
import time
import urllib.request

import cv2
import mediapipe as mp
import numpy as np
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision as mp_vision

# ── Model ────────────────────────────────────────────────────────────────────
MODEL_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "face_landmarker.task")
MODEL_URL = (
    "https://storage.googleapis.com/mediapipe-models/"
    "face_landmarker/face_landmarker/float16/1/face_landmarker.task"
)

# ── Landmark indices ─────────────────────────────────────────────────────────
R_EYE_H = (33, 133)
R_EYE_V = [(159, 145), (158, 153), (160, 144)]
L_EYE_H = (362, 263)
L_EYE_V = [(386, 374), (385, 380), (387, 373)]
R_BROW, R_EYE_REF = 105, 133   # inner eye corners — stable during blinks
L_BROW, L_EYE_REF = 334, 362
INTEROC = (33, 263)

CALIB_FRAMES = 60
TRIPLE_BLINK_WINDOW_S = 1.2


# ── Module-level helpers (pure, no state) ────────────────────────────────────
def _ensure_model():
    if not os.path.exists(MODEL_PATH):
        print("Downloading face_landmarker.task (~3 MB)...")
        urllib.request.urlretrieve(MODEL_URL, MODEL_PATH)
        print("Download complete.")


def euclidean(a, b):
    return np.linalg.norm(np.array([a.x, a.y]) - np.array([b.x, b.y]))


def eye_aspect_ratio(lm, h_pair, v_pairs):
    horizontal = euclidean(lm[h_pair[0]], lm[h_pair[1]])
    if horizontal == 0:
        return 0.0
    vertical = sum(euclidean(lm[p], lm[q]) for p, q in v_pairs)
    return vertical / (len(v_pairs) * horizontal)


def brow_ratio(lm):
    interoc = euclidean(lm[INTEROC[0]], lm[INTEROC[1]])
    if interoc == 0:
        return 0.0
    r = euclidean(lm[R_BROW], lm[R_EYE_REF])
    l = euclidean(lm[L_BROW], lm[L_EYE_REF])
    return ((r + l) / 2) / interoc


def compute_metrics(lm):
    ear = (
        eye_aspect_ratio(lm, R_EYE_H, R_EYE_V)
        + eye_aspect_ratio(lm, L_EYE_H, L_EYE_V)
    ) / 2
    br = brow_ratio(lm)
    return ear, br


# ── Detector class ───────────────────────────────────────────────────────────
class FaceGestureDetector:
    def __init__(
        self,
        model_path=MODEL_PATH,
        calib_frames=CALIB_FRAMES,
        triple_blink_window_s=TRIPLE_BLINK_WINDOW_S,
    ):
        _ensure_model()
        options = mp_vision.FaceLandmarkerOptions(
            base_options=mp_python.BaseOptions(model_asset_path=model_path),
            running_mode=mp_vision.RunningMode.VIDEO,
            num_faces=1,
            min_face_detection_confidence=0.5,
            min_face_presence_confidence=0.5,
            min_tracking_confidence=0.5,
        )
        self._landmarker = mp_vision.FaceLandmarker.create_from_options(options)
        self._t0 = time.time()
        self._calib_frames = calib_frames
        self._triple_blink_window_s = triple_blink_window_s

        self._calibration_started = False
        self._calib_ears = []
        self._calib_brows = []
        self._blink_thresh = None
        self._brow_thresh = None

        self._eye_closed = False
        self._brow_raised = False
        self._blink_times = []
        self._triple_blink_count = 0

        self._pending_brow_event = False
        self._pending_triple_event = False

        self._last_lm = None
        self._last_ear = 0.0
        self._last_br = 0.0

    # ── Pipeline ─────────────────────────────────────────────────────────────
    def process(self, frame_bgr) -> bool:
        """Run landmark detection and update state. Returns True if a face was found."""
        rgb = cv2.cvtColor(frame_bgr, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        ts_ms = int((time.time() - self._t0) * 1000)
        result = self._landmarker.detect_for_video(mp_image, ts_ms)

        if not result.face_landmarks:
            self._last_lm = None
            return False

        lm = result.face_landmarks[0]
        self._last_lm = lm
        ear, br = compute_metrics(lm)
        self._last_ear, self._last_br = ear, br

        if self._calibration_started and self._blink_thresh is None:
            self._collect_calibration(ear, br)
        elif self._blink_thresh is not None:
            self._update_gestures(ear, br)

        return True

    def is_calibrating(self) -> bool:
        return self._blink_thresh is None

    def start_calibration(self):
        if not self._calibration_started and self._blink_thresh is None:
            self._calibration_started = True
            print("Hold a neutral face for calibration (~2 s)...")

    # ── Edge-triggered queries (consume-on-read) ──────────────────────────────
    def eyebrow_raised(self) -> int:
        """Returns 1 on the frame eyebrows first cross above threshold, 0 otherwise."""
        fired = self._pending_brow_event
        self._pending_brow_event = False
        return 1 if fired else 0

    def triple_blinked(self) -> int:
        """Returns 1 on the frame the third blink fires within the window, 0 otherwise."""
        fired = self._pending_triple_event
        self._pending_triple_event = False
        return 1 if fired else 0

    # ── Overlay ───────────────────────────────────────────────────────────────
    def draw_overlay(self, frame_bgr):
        h, w = frame_bgr.shape[:2]

        if self._last_lm is not None:
            pts = {
                R_EYE_H[0], R_EYE_H[1], L_EYE_H[0], L_EYE_H[1],
                R_BROW, R_EYE_REF, L_BROW, L_EYE_REF,
            }
            for p, q in R_EYE_V + L_EYE_V:
                pts.add(p)
                pts.add(q)
            for idx in pts:
                x = int(self._last_lm[idx].x * w)
                y = int(self._last_lm[idx].y * h)
                cv2.circle(frame_bgr, (x, y), 2, (0, 255, 0), -1)

        if not self._calibration_started:
            cv2.putText(frame_bgr, "Press SPACE to start calibration",
                        (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (0, 200, 255), 2)
        elif self.is_calibrating():
            cv2.putText(frame_bgr, "Calibrating - hold neutral face",
                        (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (0, 200, 255), 2)
            progress = int(len(self._calib_ears) / self._calib_frames * (w - 20))
            cv2.rectangle(frame_bgr, (10, h - 30), (10 + progress, h - 15),
                          (0, 200, 255), -1)
        else:
            cv2.putText(frame_bgr, f"EAR: {self._last_ear:.3f}  Brow: {self._last_br:.3f}",
                        (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1)
            cv2.putText(frame_bgr, f"Triple blinks: {self._triple_blink_count}",
                        (10, 55), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1)
            if self._eye_closed:
                cv2.putText(frame_bgr, "BLINKING", (10, 85),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.65, (0, 100, 255), 2)
            if self._brow_raised:
                cv2.putText(frame_bgr, "EYEBROWS RAISED", (10, 110),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.65, (0, 255, 200), 2)

        if self._last_lm is None:
            cv2.putText(frame_bgr, "No face detected", (10, 30),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.65, (0, 0, 255), 2)

    # ── Cleanup ───────────────────────────────────────────────────────────────
    def close(self):
        self._landmarker.close()

    # ── Internals ─────────────────────────────────────────────────────────────
    def _collect_calibration(self, ear, br):
        self._calib_ears.append(ear)
        self._calib_brows.append(br)
        if len(self._calib_ears) >= self._calib_frames:
            self._blink_thresh = float(np.mean(self._calib_ears)) * 0.75
            self._brow_thresh = float(np.mean(self._calib_brows)) * 1.12
            print(f"Calibration done - EAR threshold: {self._blink_thresh:.3f}, "
                  f"Brow threshold: {self._brow_thresh:.3f}")

    def _update_gestures(self, ear, br):
        # Triple blink: track reopen timestamps, fire on third within window
        if ear < self._blink_thresh:
            self._eye_closed = True
        elif self._eye_closed:
            self._eye_closed = False
            now = time.monotonic()
            self._blink_times.append(now)
            self._blink_times = [t for t in self._blink_times
                                  if now - t <= self._triple_blink_window_s]
            if len(self._blink_times) >= 3:
                self._triple_blink_count += 1
                self._blink_times.clear()
                self._pending_triple_event = True

        # Eyebrow raise: edge-triggered, gated on eyes being open
        if not self._eye_closed and br > self._brow_thresh and not self._brow_raised:
            self._brow_raised = True
            self._pending_brow_event = True
        elif br <= self._brow_thresh:
            self._brow_raised = False
