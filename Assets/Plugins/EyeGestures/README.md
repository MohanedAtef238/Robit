# EyeGestures + Unity Integration

This folder contains the complete integration for the EyeGestures Python library into Unity.

## 📂 Files Overview

| File | Description |
|------|-------------|
| `run_eye_tracker.bat` | **Start here!** Auto-installs dependencies and runs the bridge. |
| `eyeGestures_to_unity.py` | Python script: Captures webcam -> Sends UDP JSON to Unity. |
| `EyeTrackingInput.cs` | Unity Script: Receives UDP, handles threading, & moves mouse. |
| `GazeClickHandler.cs` | Unity Script: Handles gaze-hover and blink-click interactions. |

## 🚀 Quick Start

1. **Setup Unity**:
    * Move `EyeTrackingInput.cs` to `Assets/Scripts/Utils/`.
    * Move `GazeClickHandler.cs` to `Assets/Scripts/UI/`.
    * Add `EyeTrackingInput` to a **persistent** GameObject (e.g., "InputManager").

2. **Run the Tracker**:
    * Double-click `run_eye_tracker.bat`.
    * It will install `eyeGestures` if missing and start the camera.
    * *Keep this window open.*

3. **Play in Unity**:
    * Press Play.
    * You should see `[EyeTracking] UDP server started` in the console.

## ⚙️ Configuration & Architecture

### Coordinate System (Read Carefully)

The integration handles coordinate conversion automatically:

1. **Python (OpenCV)**: Origin is **Top-Left**. Sends raw pixel coordinates (e.g., Y=0 is Top).
2. **Unity (EyeTrackingInput)**:
    * **OS Mouse (`SimulateMouseMove`)**: Uses **Top-Left** (Matches Python/Windows).
    * **Unity UI (`GazePosition`)**: Uses **Bottom-Left** (Inverted Y).

*If your UI interaction feels vertically flipped, check that you are using `EyeTrackingInput.Instance.GazePosition` and NOT raw data.*

### Thread Safety

`EyeTrackingInput.cs` runs the UDP receiver on a background thread. All public properties (`GazePosition`, `IsBlinking`) are thread-safe and can be safely accessed from Unity's `Update()` loop (Main Thread).

## 🛠 Troubleshooting

**Q: The cursor moves, but buttons don't click.**

* A: Ensure the button has a `GazeClickHandler` component.
* A: Check that `Use Blink For Click` is enabled or try hovering for 1 second.

**Q: The real mouse cursor fights the eye tracker.**

* A: This is normal if you move the mouse. The eye tracker uses `SetCursorPos` which jumps the cursor. For production, hide the OS cursor in Unity (`Cursor.visible = false`).

**Q: Error `DllNotFoundException`?**

* A: This integration uses `user32.dll` for mouse control, which works only on **Windows**. For Mac/Linux, disable `Enable Mouse Simulation`.

**Q: "Address already in use" Error?**

* A: Only run one instance of the Python script. Close any other open terminals.

## 🖥 Python Requirements

If you prefer running manual commands instead of the batch file:

```bash
pip install eyeGestures opencv-contrib-python numpy
python eyeGestures_to_unity.py
```
