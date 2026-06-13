from typing import NamedTuple

import numpy as np

# ── Landmark indices ──────────────────────────────────────────────────────────
R_EYE_INNER   = 133
R_EYE_OUTER   = 33
L_EYE_INNER   = 362
L_EYE_OUTER   = 263
R_IRIS_CENTER = 468
L_IRIS_CENTER = 473
INTEROC        = (33, 263)

# ── Defaults ──────────────────────────────────────────────────────────────────
GAZE_CALIB_FRAMES     = 60
GAZE_SENSITIVITY_UP   = 0.001   # enter-up threshold (deviation below baseline)
GAZE_SENSITIVITY_DOWN = 0.005   # enter-down threshold (deviation above baseline)
EMA_ALPHA             = 0.2     # smoothing factor — lower is smoother, more lag
HYSTERESIS_FACTOR     = 0.4     # exit threshold = enter threshold * this factor
DWELL_FRAMES          = 4       # consecutive frames before a direction is confirmed
BASELINE_DRIFT_RATE   = 0.1     # how fast baseline nudges toward neutral gaze


class GazeResult(NamedTuple):
    direction: str  # 'up', 'down', or 'straight'
    speed: int      # magnitude of deviation — higher means more extreme gaze


# ── Pure helpers ──────────────────────────────────────────────────────────────

def get_gaze_offset(landmarks) -> float:
    """Returns iris vertical offset relative to eye-corner midpoints, normalized
    by inter-ocular distance. More negative = looking up; more positive = looking down."""
    if not landmarks:
        return 0.0

    interoc_dist = abs(landmarks[INTEROC[1]].x - landmarks[INTEROC[0]].x)
    if interoc_dist == 0:
        return 0.0

    r_mid_y  = (landmarks[R_EYE_INNER].y + landmarks[R_EYE_OUTER].y) / 2
    r_offset = (landmarks[R_IRIS_CENTER].y - r_mid_y) / interoc_dist

    l_mid_y  = (landmarks[L_EYE_INNER].y + landmarks[L_EYE_OUTER].y) / 2
    l_offset = (landmarks[L_IRIS_CENTER].y - l_mid_y) / interoc_dist

    return (r_offset + l_offset) / 2


def _update_ema(current: float | None, sample: float, alpha: float) -> float:
    if current is None:
        return sample
    return current + alpha * (sample - current)


def _classify_with_hysteresis(
    deviation: float,
    confirmed: str,
    sensitivity_up: float,
    sensitivity_down: float,
    hysteresis_factor: float,
) -> str:
    """Returns the raw direction, applying hysteresis to prevent boundary chattering."""
    enter_up   = -sensitivity_up
    exit_up    = -sensitivity_up   * hysteresis_factor
    enter_down =  sensitivity_down
    exit_down  =  sensitivity_down * hysteresis_factor

    if confirmed == "up":
        return "up" if deviation < exit_up else "straight"
    if confirmed == "down":
        return "down" if deviation > exit_down else "straight"
    if deviation < enter_up:
        return "up"
    if deviation > enter_down:
        return "down"
    return "straight"


def _advance_dwell(raw: str, candidate: str, count: int) -> tuple[str, int]:
    """Increments dwell count if direction is held; resets on change.
    Returns (new_candidate, new_count)."""
    if raw == candidate:
        return candidate, count + 1
    return raw, 1


# ── Calibrator ────────────────────────────────────────────────────────────────

class GazeCalibrator:
    """Collects a neutral-gaze baseline, then classifies each frame's gaze offset."""

    def __init__(
        self,
        calib_frames: int        = GAZE_CALIB_FRAMES,
        sensitivity_up: float    = GAZE_SENSITIVITY_UP,
        sensitivity_down: float  = GAZE_SENSITIVITY_DOWN,
        ema_alpha: float         = EMA_ALPHA,
        hysteresis_factor: float = HYSTERESIS_FACTOR,
        dwell_frames: int        = DWELL_FRAMES,
        baseline_drift_rate: float = BASELINE_DRIFT_RATE,
    ):
        self._calib_frames        = calib_frames
        self._sensitivity_up      = sensitivity_up
        self._sensitivity_down    = sensitivity_down
        self._ema_alpha           = ema_alpha
        self._hysteresis_factor   = hysteresis_factor
        self._dwell_frames        = dwell_frames
        self._baseline_drift_rate = baseline_drift_rate

        self._samples    : list[float] = []
        self._baseline   : float | None = None
        self._ema        : float | None = None
        self._confirmed  : str = "straight"
        self._candidate  : str = "straight"
        self._dwell_count: int = 0

    @property
    def is_calibrated(self) -> bool:
        return self._baseline is not None

    @property
    def progress(self) -> float:
        """Calibration progress from 0.0 to 1.0."""
        if self._baseline is not None:
            return 1.0
        return len(self._samples) / self._calib_frames

    def feed(self, offset: float) -> bool:
        """Accumulate a calibration sample. Returns True when calibration completes."""
        if self._baseline is not None:
            return True
        self._samples.append(offset)
        if len(self._samples) >= self._calib_frames:
            self._baseline = float(np.mean(self._samples))
            return True
        return False

    def classify(self, offset: float) -> GazeResult:
        """Classify a gaze offset into a direction and speed.

        Applies EMA smoothing -> hysteresis thresholds -> dwell-time gating ->
        adaptive baseline drift in that order.
        """
        if self._baseline is None:
            return GazeResult("straight", 0)

        self._ema = _update_ema(self._ema, offset, self._ema_alpha)
        deviation = self._ema - self._baseline

        raw = _classify_with_hysteresis(
            deviation, self._confirmed,
            self._sensitivity_up, self._sensitivity_down,
            self._hysteresis_factor,
        )

        self._candidate, self._dwell_count = _advance_dwell(
            raw, self._candidate, self._dwell_count
        )
        if self._dwell_count >= self._dwell_frames:
            self._confirmed = self._candidate

        speed = int(abs(deviation) * 500) if self._confirmed != "straight" else 0

        if self._confirmed == "straight":
            self._baseline += self._baseline_drift_rate * (self._ema - self._baseline)

        return GazeResult(self._confirmed, speed)
