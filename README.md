# Robit

> A low-cost, multimodal, hands-free Windows OS wrapper that fuses **eye-gaze tracking** with **EMG-based jaw-clench triggers** for reliable assistive computer control.

---

## Table of Contents

- [System Architecture Overview](#system-architecture-overview)
- [Bootstrap & Scene Lifecycle](#bootstrap--scene-lifecycle)
- [Camera Pipeline](#camera-pipeline)
- [Gaze Tracking (Black Box)](#gaze-tracking-black-box)
- [EMG Input (Black Box)](#emg-input-black-box)
- [Virtual Input State & Cursor Driver](#virtual-input-state--cursor-driver)
- [UI Automation Subsystem](#ui-automation-subsystem)
- [Process Lifecycle Management](#process-lifecycle-management)
- [IPC Architecture — Rejected Alternatives](#ipc-architecture--rejected-alternatives)
- [Windows API & Transparency Layer](#windows-api--transparency-layer)
- [Macro Button System (MVVM)](#macro-button-system-mvvm)
- [Component Reference](#component-reference)

---

## System Architecture Overview

Robit is split across two tiers: a **Unity C# frontend** that owns the UI, OS windowing, and input composition, and a set of **headless Python/C# executables** that own the machine learning inference and webcam capture. All communication between tiers is performed via shared memory (video frames) and UDP datagrams (coordinates and click events).

```mermaid
graph TD
    subgraph HARDWARE["Hardware"]
        CAM["Webcam"]
        HEADBAND["ESP32-C3 Wearable\nHeadband (EMG)"]
    end

    subgraph PYTHON["Python Backends (Headless Executables)"]
        SHARE_CAM["share_camera.exe\nOpenCV / DirectShow"]
        GAZE_BRIDGE["unity_gaze_bridge.exe\nMediaPipe Face Mesh"]
        EMG_BRIDGE["unity_emg_bridge.exe\nTensorFlow / EMG ML Model"]
    end

    subgraph UNITY["Unity C# Frontend"]
        SCC["SharedCameraCapture.cs\nMMF Reader + Preview"]
        GFR["GazeFollowerRunner.cs\nUDP Receiver"]
        EPR["EmgPredictionRunner.cs\nUDP Receiver"]
        VIS["VirtualInputState\n(Singleton)"]
        VPD["VirtualPointerDriver.cs\nOS Cursor Emulator"]
        UAR["UiAutomationRunner.cs\nWebSocket + Stdin Bridge"]
    end

    subgraph OS["Windows OS"]
        CURSOR["System Cursor"]
        CLICK["Mouse Click Events"]
        UIAUTO["Windows UIAutomation API"]
    end

    CAM -->|"DirectShow"| SHARE_CAM
    HEADBAND -->|"Serial / COM4"| EMG_BRIDGE

    SHARE_CAM -->|"MMF: RobitCameraFrame\n(double-buffered RGBA32)"| SCC
    SHARE_CAM -->|"MMF: RobitCameraFrame\n(same buffer, zero-copy read)"| GAZE_BRIDGE

    GAZE_BRIDGE -->|"UDP: (x, y) screen coords"| GFR
    EMG_BRIDGE -->|"UDP: 0 or 1 binary signal"| EPR

    GFR -->|"SetGazePosition()"| VIS
    EPR -->|"SetEmgPrediction()"| VIS

    VIS -->|"GazePosition"| VPD
    VIS -->|"IsEmgActive"| VPD

    VPD -->|"SetCursorPos()"| CURSOR
    VPD -->|"SendInput() LeftDown/Up"| CLICK
    VPD -->|"WebSocket: getClosest"| UAR

    UAR -->|"Stdin JSON cmds"| UIAUTO
    UIAUTO -->|"CMD_RESPONSE JSON"| UAR
```

---

## Bootstrap & Scene Lifecycle

Every major runner is a persistent singleton. They bootstrap themselves before any scene loads using `[RuntimeInitializeOnLoadMethod]` and call `DontDestroyOnLoad()` to survive scene transitions.

```mermaid
sequenceDiagram
    participant Engine as Unity Engine
    participant BPR as BaseProcessRunner
    participant GFR as GazeFollowerRunner
    participant EPR as EmgPredictionRunner
    participant VPD as VirtualPointerDriver
    participant SCC as SharedCameraCapture

    Engine->>GFR: RuntimeInitializeOnLoadMethod (BeforeSceneLoad)
    GFR->>BPR: Awake() → SetParent(null) → DontDestroyOnLoad
    GFR->>SCC: EnsureSpawned()
    SCC->>SCC: DontDestroyOnLoad

    Engine->>EPR: RuntimeInitializeOnLoadMethod (BeforeSceneLoad)
    EPR->>BPR: Awake() → SetParent(null) → DontDestroyOnLoad

    Engine->>VPD: RuntimeInitializeOnLoadMethod (BeforeSceneLoad)
    VPD->>VPD: Awake() → DontDestroyOnLoad

    Note over GFR,VPD: All singletons now survive every subsequent scene load
```

---

## Camera Pipeline

The webcam is owned **exclusively** by `share_camera.exe` to avoid handle conflicts between Unity and Python. Unity's `SharedCameraCapture.cs` and the gaze bridge both read from the same **Memory Mapped File**, making this a zero-copy, zero-conflict distribution pattern.

```mermaid
sequenceDiagram
    participant SCC as SharedCameraCapture (Unity)
    participant EXE as share_camera.exe (Python/OpenCV)
    participant MMF as "MMF: RobitCameraFrame"
    participant GAZE as unity_gaze_bridge.exe

    SCC->>EXE: Process.Start() -- device 0 --width 640 --height 480 --fps 30
    EXE->>MMF: CreateOrOpen("RobitCameraFrame")
    EXE->>MMF: Write MAGIC, VERSION, width, height, frameId each frame
    EXE->>MMF: Write pixel data to active double-buffer slot (0 or 1)

    loop Every Unity frame
        SCC->>MMF: ReadInt32(0) — check MAGIC
        SCC->>MMF: ReadInt64(20) — check frameId (skip if unchanged)
        SCC->>MMF: ReadArray(offset, pixels) — CPU-only, no GPU readback
        SCC->>SCC: SetPixels32() → Apply() → expose PreviewTexture
    end

    GAZE->>MMF: OpenExisting("RobitCameraFrame")
    loop Every inference tick
        GAZE->>MMF: Read pixels from active buffer (same zero-copy read)
        GAZE->>GAZE: Run MediaPipe Face Mesh inference
    end
```

**MMF Buffer Layout:**

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | `MAGIC` — `0x524F4254` (`"ROBT"`) |
| 4 | 4 | `VERSION` — currently `1` |
| 8 | 4 | `width` |
| 12 | 4 | `height` |
| 16 | 4 | `format` — `1 = RGBA32` |
| 20 | 8 | `frameId` — monotonically increasing `int64` |
| 28 | 8 | `timestamp` — Windows FILETIME ticks |
| 36 | 4 | `activeBuffer` — `0` or `1` (double-buffered) |
| 40+ | ... | pixel data — two slots of `MAX_WIDTH × MAX_HEIGHT × 4` |

---

## Gaze Tracking (Black Box)

From Unity's perspective, the gaze bridge is an opaque executable. `GazeFollowerRunner.cs` only cares about its inputs and outputs.

```mermaid
flowchart LR
    subgraph UNITY_GAZE["Unity — GazeFollowerRunner"]
        direction TB
        A["Process.Start(unity_gaze_bridge.exe\n--port {dynamic} --mode run-saved\n--profile {id})"]
        B["UdpClient.ReceiveAsync()\n(background thread)"]
        C["ConcurrentQueue&lt;Vector2&gt;\ngazePacketQueue"]
        D["Update() — drain queue\nkeep only LATEST packet"]
        E["VirtualInputState\n.SetGazePosition(x, y)"]
    end

    MMF["MMF: RobitCameraFrame"] -->|"read frames"| EXE
    EXE["unity_gaze_bridge.exe\nMediaPipe Face Mesh ⬛"] -->|"UDP (x,y)"| B
    A --> EXE
    B --> C --> D --> E
```

**Why only the latest packet?** Gaze coordinates arrive faster than the 60 FPS Unity loop. Replaying stale packets would cause the cursor to lag behind the user's eye. The queue is drained completely each frame and only the newest value is applied.

---

## EMG Input (Black Box)

`EmgPredictionRunner.cs` follows the same pattern. The EMG bridge reads raw serial data from the ESP32 wearable, runs an ML classifier, and sends a binary 0/1 UDP signal.

```mermaid
flowchart LR
    subgraph UNITY_EMG["Unity — EmgPredictionRunner"]
        direction TB
        A2["Process.Start(unity_emg_bridge.exe\n--port {dynamic} --com-port COM4)"]
        B2["UdpClient.ReceiveAsync()"]
        C2["ConcurrentQueue&lt;bool&gt;\nemgPacketQueue"]
        D2["Update() — drain queue"]
        E2["VirtualInputState\n.SetEmgPrediction(active, confidence)"]
    end

    ESP["ESP32-C3 Wearable (EMG Sensors)"]
    EXE2["unity_emg_bridge.exe\nTF ML Classifier ⬛"]

    ESP -->|"COM4 Serial\n115200 baud"| EXE2
    EXE2 -->|"EMG_STATUS:OK"| B2
    EXE2 -->|"UDP: '1' or '0'"| B2
    A2 --> EXE2
    B2 --> C2 --> D2 --> E2
```

On process exit, `OnProcessExited` enqueues a `false` signal so the cursor never gets stuck in a held-down click state after the bridge dies.

---

## Virtual Input State & Cursor Driver

`VirtualInputState` is the fusion point. It receives gaze positions and EMG predictions independently and exposes them as a unified interface to `VirtualPointerDriver`.

```mermaid
flowchart TD
    subgraph FUSION["VirtualInputState (Singleton)"]
        GS["GazePosition : Vector2"]
        EA["IsEmgActive : bool"]
        HC["HasGazePosition : bool"]
    end

    subgraph DRIVER["VirtualPointerDriver (Singleton, DontDestroyOnLoad)"]
        direction TB
        SM["Smoothing\nVector2.Lerp(smoothed, target,\nTime.deltaTime × smoothingSpeed)"]
        MC["UpdateWindowsCursor()\nWin32Interop.SetCursorPos(x, y)"]
        MB["UpdateMouseButton()\nWin32Interop.SendInput()"]
        DRAG["Drag Detection\nif held > dragToMessageTime:\nSendGetClosest() via WebSocket"]
    end

    GFR["GazeFollowerRunner"] -->|"SetGazePosition"| GS
    EPR["EmgPredictionRunner"] -->|"SetEmgPrediction"| EA
    GS --> SM --> MC
    EA --> MB
    MB -->|"click duration ≥ minClickHoldTime\n& < maxClickDuration"| CLICK["OS LeftDown + LeftUp"]
    MB --> DRAG
    DRAG -->|"WebSocket JSON"| UAR["UiAutomationRunner"]
```

**Click vs. Drag logic in `VirtualPointerDriver`:**
- Signal starts → record `emgStartTime`
- Signal ends after `minClickHoldTime` → fire `LeftDown` + `LeftUp` (intentional tap)
- Signal ends before `minClickHoldTime` → discard (electrical noise)
- Signal held past `dragToMessageTime` (default 2s) → fire `getClosest` WebSocket message to the UI Automation server and release the click

---

## UI Automation Subsystem

When a user performs a sustained gaze-and-hold gesture, Robit surfaces a smart "closest UI element" selection overlay via the Windows UIAutomation API rather than relying on the user hitting a pixel-perfect target.

```mermaid
sequenceDiagram
    participant VPD as VirtualPointerDriver
    participant WS as WebSocket (ws://127.0.0.1:8181)
    participant UAR as UiAutomationRunner (Unity)
    participant PROC as Robit-UI-Automation.exe (.NET)
    participant WIN as Windows UIAutomation API

    VPD->>WS: {"type":"getClosest"}
    WS->>UAR: OnMessage received
    UAR->>PROC: stdin JSON command
    PROC->>WIN: FindAll(TreeScope.Descendants, condition)
    WIN-->>PROC: List of AutomationElement near cursor
    PROC-->>UAR: CMD_RESPONSE: [{name, rect, type}, ...]
    UAR->>UAR: mainThreadContext.Post → OnMessageReceived event
    Note over UAR: Unity UI renders selection overlay\nUser gaze-targets & jaw-clenches to confirm
```

---

## Process Lifecycle Management

```mermaid
flowchart TD
    START(( )) -->|Awake, DontDestroyOnLoad| IDLE[Idle]
    
    IDLE -->|StartRunner called| STARTING[Starting]
    STARTING -->|Process.Start OK\nChildProcessTracker.AddProcess| RUNNING[Running]
    
    RUNNING -->|stdout/stderr forwarded\nto RobitLogger| RUNNING
    RUNNING -->|process.Exited event| DEAD[Dead]
    RUNNING -->|StopRunner called\nOnApplicationQuit / OnDestroy| TERMINATING[Terminating]
    
    DEAD -->|OnProcessExited\nenqueue reset value| IDLE
    TERMINATING -->|process.Kill\nUnhookAndDispose\nInstance = null| END(( ))
```

**Key design decisions:**
- `ChildProcessTracker.cs` creates a Windows **Job Object** and assigns every spawned process to it. If Unity crashes (e.g. killed via Task Manager), the OS automatically terminates all child processes in the job, eliminating zombie processes entirely.
- `BaseProcessRunner<T>` calls `SetParent(null)` before `DontDestroyOnLoad` because Unity only preserves **root** GameObjects across scene loads. A child object would be silently destroyed, severing the MMF and UDP connections.

---

## IPC Architecture — Rejected Alternatives

| Alternative | Why it was rejected |
|---|---|
| **Unity Barracuda / Sentis (C# ML)** | Lacks support for custom TensorFlow ops used by our MediaPipe and EMG models. Converting to ONNX produced unsupported-layer errors and measurable precision loss. |
| **Python for Unity (embedded interpreter)** | Python's GIL and heavy inference calls block the Unity thread pool. Even on a background thread, GIL contention causes frame-time spikes that destroy our 16.666 ms budget. |
| **TCP Sockets instead of UDP** | TCP guarantees ordered delivery — exactly the wrong property for a live gaze stream. A stale (x, y) packet queued behind newer ones causes visible cursor lag. UDP lets us discard stale data intentionally. |
| **HTTP / REST API** | Round-trip latency of HTTP is measured in ms to tens of ms per request. At 60 Hz the gaze bridge fires ~60 position updates per second, making HTTP completely unsuitable. |
| **Unity WebCamTexture (built-in)** | Creates a GPU→CPU readback stall every frame. Also holds an exclusive DirectShow handle that blocks the Python gaze bridge from opening the same webcam. The external `share_camera.exe` + MMF design eliminates both problems. |
| **Shared memory direct from Unity** | Unity's C# runtime cannot efficiently write raw pixel arrays to MMF without unsafe buffer copies. Having `share_camera.exe` own the camera entirely avoids the Unity rendering pipeline dependency and ensures a full 1280×720 feed at zero frame drops. |

---

## Windows API & Transparency Layer

Robit renders as a transparent, always-on-top, click-through overlay window using direct `user32.dll` and `Dwmapi.dll` P/Invokes.

```mermaid
flowchart TD
    subgraph WINAPI["Win32 API Calls on Startup"]
        A["GetActiveWindow() → HWND"]
        B["DwmExtendFrameIntoClientArea(HWND, MARGINS{-1})\nExtend DWM glass to full window → true transparency"]
        C["SetWindowLong(HWND, GWL_EXSTYLE,\nWS_EX_LAYERED | WS_EX_TRANSPARENT)\nEnable layered + click-through"]
        D["SetWindowPos(HWND, HWND_TOPMOST, ...)\nForce always-on-top"]
    end

    subgraph TRANSPARENCY["Transparency.cs — Per-Frame Loop"]
        E["GetCursorPos() → global POINT\n(works even in click-through mode)"]
        F["ScreenToClient(HWND, point)\nConvert to Unity window coords"]
        G["EventSystem.RaycastAll()\nAny Unity UI hit?"]
        H_YES["SetWindowLong(..., WS_EX_LAYERED)\nRemove WS_EX_TRANSPARENT\n→ clicks reach Unity"]
        H_NO["SetWindowLong(..., WS_EX_LAYERED | WS_EX_TRANSPARENT)\nRestore click-through\n→ clicks pass to app below"]
    end

    A --> B --> C --> D
    E --> F --> G
    G -->|"Yes (over UI)"| H_YES
    G -->|"No"| H_NO
```

**The click-through paradox:** When `WS_EX_TRANSPARENT` is active, Windows does not deliver any mouse events to Unity — including `OnPointerEnter`. The only way to detect hover is to poll the cursor position ourselves via `GetCursorPos()`, which always works regardless of window style, then raycasting from Unity's side.

---

## Macro Button System (MVVM)

The overlay macro panel uses a strict **Model-View-ViewModel** architecture via Unity UI Toolkit.

```mermaid
classDiagram
    class MacroViewModel {
        +Groups : MacroGroup[] (static)
        +IsOpen : bool
        +OnMenuToggled : Action~bool~
        +OnGroupChanged : Action~MacroGroup~
        +Open()
        +Close()
        +PrevGroup()
        +NextGroup()
        +GetCurrentGroup() MacroGroup
    }

    class MacroButtonController {
        -viewModel : MacroViewModel
        +OnEnable()
        +OnDisable()
        -OnMenuToggled(bool isOpen)
        -OnGroupChanged(MacroGroup group)
        -RevealGroup(MacroGroup group)
    }

    class IMacroAction {
        <<interface>>
        +ActionId : string
        +DisplayName : string
        +Execute()
    }

    class MacroActionFactory {
        +Create(MacroActionType) IMacroAction
    }

    class IInputProvider {
        <<interface>>
        +Attach(VisualElement, Action)
        +Detach(VisualElement)
    }

    class PointerInputProvider {
        +Attach(VisualElement, Action)
        +Detach(VisualElement)
    }

    MacroButtonController --> MacroViewModel : binds to events
    MacroButtonController --> MacroActionFactory : creates actions
    MacroButtonController --> IInputProvider : attaches to buttons
    MacroActionFactory --> IMacroAction : instantiates
    IInputProvider <|.. PointerInputProvider
```

- **Adding a new action**: implement `IMacroAction`, add to `MacroActionType` enum, add a case in `MacroActionFactory.Create()`.
- **Adding a new input modality** (e.g. gaze dwell): implement `IInputProvider` and swap it in `MacroButtonController.OnEnable()`.

---

## Component Reference

| File | Location | Role |
|---|---|---|
| `BaseProcessRunner.cs` | `OverlaySystem/` | Generic singleton base — process start, I/O redirect, teardown |
| `BaseUdpProcessRunner.cs` | `OverlaySystem/` | Extends base with UDP port binding and async receive loop |
| `ChildProcessTracker.cs` | `OverlaySystem/` | Windows Job Object — guarantees child process cleanup on crash |
| `SharedCameraCapture.cs` | `OverlaySystem/` | Launches `share_camera.exe`, reads MMF, exposes `Texture2D` |
| `GazeFollowerRunner.cs` | `OverlaySystem/` | Manages `unity_gaze_bridge.exe`, UDP → `VirtualInputState` |
| `EmgPredictionRunner.cs` | `Input/` | Manages `unity_emg_bridge.exe`, UDP → `VirtualInputState` |
| `VirtualPointerDriver.cs` | `Input/` | Reads `VirtualInputState`, drives OS cursor + click via P/Invoke |
| `UiAutomationRunner.cs` | `OverlaySystem/` | Bridges WebSocket from `VirtualPointerDriver` to `.NET` UIAutomation server |
| `GazeCalibrationController.cs` | `OverlaySystem/` | Orchestrates gaze calibration phases via UDP, persists profiles |
| `MacroViewModel.cs` | `MacroSystem/` | MVVM ViewModel — macro group state and menu open/close |
| `MacroButtonController.cs` | `MacroSystem/` | MVVM View — binds UI Toolkit elements to ViewModel events |
| `MacroActionFactory.cs` | `MacroSystem/` | Factory — creates `IMacroAction` instances from enum |
| `Win32Interop.cs` | `Utils/` | All P/Invoke declarations (`SetCursorPos`, `SendInput`, etc.) |
| `WindowManager.cs` | `Utils/` | Window HWND caching + extended style helpers |
| `RobitLogger.cs` | `Utils/` | Thread-safe Unity log wrapper used across all systems |
