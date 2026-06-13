# Eye Gaze Scroll

Scroll up and down using eye gaze detected via webcam and MediaPipe face landmarks.

## Files

| File | Purpose |
|---|---|
| `eye_movement.py` | Pure library — gaze math, calibration, classification. No I/O. |
| `main.py` | Entry point — camera loop, printing, keyboard handling. |

## How it works

1. **Calibration** — the user holds a neutral gaze for ~2 seconds while the baseline iris position is recorded.
2. **Classification** — each frame's iris position is compared to the baseline. Deviations beyond a threshold are classified as `up` or `down` along with a speed value.
3. **Activation** — triple-blink toggles scroll mode on/off.

### Signal pipeline (per frame)

```
raw iris offset
  → EMA smoothing        (reduces per-frame noise)
  → hysteresis threshold (prevents chattering at the boundary)
  → dwell-time gate      (ignores flickers shorter than N frames)
  → adaptive baseline    (corrects for posture drift over time)
  → GazeResult(direction, speed)
```

## Running

```bash
cd Scrolling
python main.py
```

### Controls

| Key | Action |
|---|---|
| `Space` | Start calibration |
| Triple blink | Toggle scroll mode on/off |
| `q` | Quit |

## Configuration

All tuneable constants are at the top of `eye_movement.py`:

| Constant | Default | Effect |
|---|---|---|
| `GAZE_CALIB_FRAMES` | `60` | Frames collected for baseline (~2 s at 30 fps) |
| `GAZE_SENSITIVITY_UP` | `0.001` | Minimum upward deviation to register |
| `GAZE_SENSITIVITY_DOWN` | `0.005` | Minimum downward deviation to register |
| `EMA_ALPHA` | `0.2` | Smoothing strength — lower = smoother, more lag |
| `HYSTERESIS_FACTOR` | `0.4` | Exit threshold as a fraction of the enter threshold |
| `DWELL_FRAMES` | `4` | Frames a direction must be held before confirming |
| `BASELINE_DRIFT_RATE` | `0.1` | How fast the baseline adapts to posture changes |

## Using `eye_movement.py` as a library

```python
from eye_movement import GazeCalibrator, get_gaze_offset

cal = GazeCalibrator()

# During calibration phase — call until cal.is_calibrated is True
cal.feed(get_gaze_offset(landmarks))

# During active use
result = cal.classify(get_gaze_offset(landmarks))
print(result.direction, result.speed)  # e.g. "up" 12
```

`GazeResult` is a `NamedTuple` with two fields:

| Field | Type | Values |
|---|---|---|
| `direction` | `str` | `"up"`, `"down"`, `"straight"` |
| `speed` | `int` | `0` when straight; higher = more extreme gaze |
