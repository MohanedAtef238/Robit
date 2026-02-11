# Robit

### This is a simple readme to record progress and current findings, as to make sure we do not forget what each file was made for as progress happens on sparesly spaced working periods

---

## Table of Contents

- [Architecture](#current-architecture-----for-note-keeping)
- [Windows API Integration](#windows-api-integration)
- [LNK File Format Constants](#lnk-file-format-constants)
- [Component Reference](#component-reference)

---

## Current Architecture --- for note keeping

Mock_OS uses a **two-scene architecture**:

```
┌─────────────────────────────────────────────────────────────────┐
│                         MainScene                                │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐     │
│  │DesktopParser│───►│AppLauncherUI │───►│   App Cards     │     │
│  │ (scan .lnk) │    │ (build grid) │    │ (click to launch)│    │
│  └─────────────┘    └──────────────┘    └────────┬────────┘     │
└──────────────────────────────────────────────────┼──────────────┘
                                                   │ User clicks app
                                                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                       OverlayScene                               │
│  ┌──────────────┐   ┌─────────────┐   ┌────────────────┐        │
│  │OverlayManager│   │ Transparency│   │ UIClickHandler │        │
│  │(window size) │   │(click-thru) │   │(hover toggles) │        │
│  └──────────────┘   └─────────────┘   └────────────────┘        │
│                                                                  │
│  ┌──────────────┐                                               │
│  │  HomeButton  │ ← Returns to MainScene                        │
│  └──────────────┘                                               │
└─────────────────────────────────────────────────────────────────┘
```

**Data Flow:**

1. `DesktopParser` scans `.lnk` files → filters to whitelist → fetches icons
2. `AppLauncherUI` generates clickable cards for each shortcut
3. User clicks a card → `AppLauncher.LaunchApplication()` starts the process
4. Scene transitions to `OverlayScene` with transparent, always-on-top window
5. `HomeButton` returns to `MainScene` and closes the launched app

---

## Windows API Integration

The overlay system relies on Windows-specific APIs via P/Invoke. Here's a breakdown of every constant and function used:

### Window Style Constants

```csharp
const int GWL_EXSTYLE = -20;
```

**Purpose:** Index parameter for `GetWindowLong`/`SetWindowLong` functions.  
**Value `-20`:** Retrieves or sets the *extended* window styles (as opposed to `GWL_STYLE = -16` for regular styles).

---

```csharp
const uint WS_EX_LAYERED = 0x00080000;
```

**Purpose:** Creates a "layered window" that supports transparency and alpha blending.  
**Required for:** Using `SetLayeredWindowAttributes` or `UpdateLayeredWindow` to make windows transparent.  
**Hex breakdown:** Bit 19 is set (2^19 = 524288 = 0x80000).

---

```csharp
const uint WS_EX_TRANSPARENT = 0x00000020;
```

**Purpose:** Makes the window "click-through" — mouse events pass through to windows below.  
**How it works:** The window is excluded from hit-testing, so clicks go to whatever is beneath it.  
**Hex breakdown:** Bit 5 is set (2^5 = 32 = 0x20).

---

```csharp
private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
```

**Purpose:** Special handle value for `SetWindowPos` that places the window above all non-topmost windows.  
**Alternative values:**

- `HWND_NOTOPMOST (−2)` – Removes topmost status
- `HWND_TOP (0)` – Top of Z-order (but not topmost)
- `HWND_BOTTOM (1)` – Bottom of Z-order

---

```csharp
const uint SWP_SHOWWINDOW = 0x0040;
```

**Purpose:** Flag for `SetWindowPos` that displays the window after repositioning.  
**Other common flags:**

- `SWP_NOSIZE (0x0001)` – Retains current size
- `SWP_NOMOVE (0x0002)` – Retains current position
- `SWP_NOZORDER (0x0004)` – Retains current Z-order

---

```csharp
const uint LWA_COLORKEY = 0x00000001;
```

**Purpose:** Used with `SetLayeredWindowAttributes` to specify a color that becomes transparent.  
**Related constant:** `LWA_ALPHA (0x02)` – Uses alpha value for whole-window transparency.

---

### MARGINS Structure (DWM)

```csharp
private struct MARGINS
{
    public int cxLeftWidth;
    public int cxRightWidth;
    public int cyTopHeight;
    public int cyBottomHeight;
}
```

**Purpose:** Defines the margins for the glass frame extended into the client area.  
**Special value:** Setting `cxLeftWidth = -1` extends glass to cover the entire window (full transparency).

---

### Win32 API Functions

```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetActiveWindow();
```

**Purpose:** Retrieves the handle of the currently active (focused) window.  
**Returns:** `HWND` of the Unity application window.

---

```csharp
[DllImport("user32.dll")]
private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
```

**Purpose:** Changes window attributes (styles, extended styles, etc.).  
**Parameters:**

- `hWnd` – Window handle
- `nIndex` – Which attribute to change (`GWL_EXSTYLE = -20`)
- `dwNewLong` – New value (combination of `WS_EX_*` flags)

---

```csharp
[DllImport("user32.dll")]
private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, 
    int X, int Y, int cx, int cy, uint uFlags);
```

**Purpose:** Changes the size, position, and Z-order of a window.  
**Parameters:**

- `hWndInsertAfter` – Z-order (`HWND_TOPMOST = -1` for always-on-top)
- `X, Y` – New position
- `cx, cy` – New width and height
- `uFlags` – Combination of `SWP_*` flags

---

```csharp
[DllImport("Dwmapi.dll")]
private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
```

**Purpose:** Extends the DWM (Desktop Window Manager) glass frame into the client area.  
**Used for:** Creating true transparency (no background color).  
**Note:** Only works when DWM composition is enabled (Windows Vista+).

---

### How They Work Together

```csharp
void Start()
{
    hWnd = GetActiveWindow();
    
    // Step 1: Extend glass frame to cover entire window (enables transparency)
    MARGINS margins = new MARGINS { cxLeftWidth = -1 };
    DwmExtendFrameIntoClientArea(hWnd, ref margins);
    
    // Step 2: Enable layered window + click-through
    SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
    
    // Step 3: Keep window always on top
    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, 0);
}
```

---

## LNK File Format Constants

The `.lnk` file format (Windows shortcuts) follows Microsoft's **MS-SHLLINK** specification. These constants decode the binary structure:

### LinkFlags (Offset 0x14 in header)

Bitmask indicating which optional structures are present in the file:

| Constant | Value | Meaning |
|----------|-------|---------|
| `HasLinkTargetIdList` | `0x00000001` | File contains a `LinkTargetIDList` structure |
| `HasLinkInfo` | `0x00000002` | File contains a `LinkInfo` structure |
| `HasName` | `0x00000004` | File contains a `NAME_STRING` (description) |
| `HasRelativePath` | `0x00000008` | File contains a `RELATIVE_PATH` string |
| `HasWorkingDir` | `0x00000010` | File contains a `WORKING_DIR` string |
| `HasArguments` | `0x00000020` | File contains command-line `COMMAND_LINE_ARGUMENTS` |
| `HasIconLocation` | `0x00000040` | File contains an `ICON_LOCATION` string |
| `IsUnicode` | `0x00000080` | Strings are encoded as Unicode (vs ANSI) |

**Usage in code:**

```csharp
var linkFlags = BitConverter.ToInt32(buffer, 0);

if ((linkFlags & LinkFlags.HasLinkTargetIdList) == LinkFlags.HasLinkTargetIdList)
    ParseTargetIDList(stream);  // Skip or parse the ID list

if ((linkFlags & LinkFlags.HasLinkInfo) == LinkFlags.HasLinkInfo)
    ParseLinkInfo(stream);  // Extract the target path
```

---

### FileAttributes (Offset 0x18 in header)

Attributes of the target file (mirrors Windows file attributes):

| Constant | Value | Meaning |
|----------|-------|---------|
| `ReadOnly` | `0x0001` | Target is read-only |
| `Hidden` | `0x0002` | Target is hidden |
| `System` | `0x0004` | Target is a system file |
| `Directory` | `0x0010` | Target is a directory |
| `Archive` | `0x0020` | Target has archive attribute |
| `Normal` | `0x0080` | No other attributes set |
| `Temporary` | `0x0100` | Temporary file |
| `Compressed` | `0x0800` | Compressed file |
| `Encrypted` | `0x4000` | Encrypted file |

**Usage in code:**

```csharp
var fileAttrFlags = BitConverter.ToInt32(buffer, 0);
IsDirectory = (fileAttrFlags & FileAttributes.Directory) == FileAttributes.Directory;
```

---

### LinkInfoFlags

Indicates where the target is located:

| Constant | Value | Meaning |
|----------|-------|---------|
| `VolumeIDAndLocalBasePath` | `1` | Target is on a local volume |
| `CommonNetworkRelativeLinkAndPathSuffix` | `2` | Target is on a network share |

---

### VirtualKeys (Hotkey parsing)

Windows virtual key codes for parsing shortcut hotkeys. The hotkey field is 2 bytes:

- **Low byte:** The key code (A-Z, F1-F24, etc.)
- **High byte:** Modifier flags

**Modifier flags:**

```csharp
HOTKEYF_SHIFT   = 1   // Shift key
HOTKEYF_CONTROL = 2   // Ctrl key
HOTKEYF_ALT     = 4   // Alt key
```

---

## Component Reference

### Core Components

#### `DesktopParser.cs`

**Purpose:** Scans Windows desktop folders for `.lnk` shortcuts and filters to allowed apps.

**Key fields:**

```csharp
public List<ShortcutInfo> shortcuts = new List<ShortcutInfo>();
public bool parsingComplete = false;

private static readonly Dictionary<string, string> IconUrls;   // App name → icon URL
private static readonly HashSet<string> AllowedApps;           // Whitelist
```

**Workflow:**

```csharp
IEnumerator ParseShortcuts()
{
    // 1. Get desktop paths
    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    var publicDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    // 2. Find all .lnk files
    shortcutFiles.AddRange(Directory.GetFiles(desktopPath, "*.lnk"));
    shortcutFiles.AddRange(Directory.GetFiles(publicDesktopPath, "*.lnk"));

    // 3. Parse each shortcut
    foreach (var file in shortcutFiles)
    {
        var shortcut = new WinShortcut(file);           // Parse .lnk binary
        
        if (!IsAllowedApp(shortcutName, exeName))       // Check whitelist
            continue;
            
        string iconUrl = GetIconUrl(shortcutName, exeName);  // Get icon
        yield return StartCoroutine(FetchIconAndAddShortcut(...));
    }
    
    parsingComplete = true;
}
```

---

#### `AppLauncher.cs`

**Purpose:** Singleton that launches external applications and manages the current process.

**Key code:**

```csharp
public static AppLauncher Instance;
private Process currentProcess;

private void Awake()
{
    if (Instance == null)
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);  // Persist across scene changes
    }
    else
    {
        Destroy(gameObject);
    }
}

public void LaunchApplication(string path, string workingDirectory)
{
    // Close any existing app first
    if (currentProcess != null && !currentProcess.HasExited)
    {
        currentProcess.CloseMainWindow();
        currentProcess.Dispose();
    }

    // Start new process
    ProcessStartInfo startInfo = new ProcessStartInfo(path);
    if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
        startInfo.WorkingDirectory = workingDirectory;

    currentProcess = Process.Start(startInfo);
    
    // Switch to overlay mode
    SceneManager.LoadScene("OverlayScene");
}

public void CloseCurrentApp()
{
    if (currentProcess != null && !currentProcess.HasExited)
    {
        currentProcess.CloseMainWindow();
        currentProcess.Dispose();
        currentProcess = null;
    }
}
```

---

#### `OverlayManager.cs`

**Purpose:** Positions and sizes the overlay window on scene load.

```csharp
void Start()
{
    #if !UNITY_EDITOR
    hWnd = GetActiveWindow();
    
    // Size window to 70% of screen
    int screenWidth = Screen.currentResolution.width;
    int screenHeight = Screen.currentResolution.height;
    int windowWidth = (int)(screenWidth * 0.7f);
    int windowHeight = (int)(screenHeight * 0.7f);

    // Position at top-left, always on top
    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, windowWidth, windowHeight, SWP_SHOWWINDOW);
    #endif
}
```

---

### UI Components

#### `AppLauncherUI.cs`

**Purpose:** Generates the app card grid from parsed shortcuts.

**Key workflow:**

```csharp
IEnumerator Start()
{
    statusText.text = "Initializing...";
    
    // Load prefab
    appCardPrefab = Resources.Load<GameObject>("AppCardPrefab");
    
    // Wait for parsing to complete (with timeout)
    statusText.text = "Scanning desktops for shortcuts...";
    while (!desktopParser.parsingComplete && elapsed < timeout)
    {
        yield return null;
        elapsed += Time.deltaTime;
    }
    
    GenerateAppCards(desktopParser.shortcuts);
}

void GenerateAppCards(List<ShortcutInfo> shortcuts)
{
    foreach (var shortcut in shortcuts)
    {
        GameObject cardInstance = Instantiate(appCardPrefab, cardContainer);
        
        RawImage iconImage = cardInstance.transform.Find("Icon").GetComponent<RawImage>();
        Text nameText = cardInstance.transform.Find("AppName").GetComponent<Text>();
        Button button = cardInstance.GetComponent<Button>();

        nameText.text = shortcut.Name;
        iconImage.texture = shortcut.Icon;
        
        button.onClick.AddListener(() => 
            AppLauncher.Instance.LaunchApplication(shortcut.TargetPath, shortcut.WorkingDirectory));
    }
}
```

---

#### `UIClickHandler.cs`

**Purpose:** Toggles click-through when hovering over UI elements.

> **Note:** This script uses Unity's event system which has a limitation - see `Transparency.cs` for the full solution using Windows API polling.

```csharp
public class UIClickHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Transparency transparency;

    void Start()
    {
        transparency = Camera.main.GetComponent<Transparency>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transparency.SetClickThrough(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transparency.SetClickThrough(true);
    }
}
```

---

#### `HomeButton.cs`

**Purpose:** Returns to MainScene and closes the launched application.

```csharp
void GoHome()
{
    if (AppLauncher.Instance != null)
    {
        AppLauncher.Instance.CloseCurrentApp();  // Terminate launched process
    }
    SceneManager.LoadScene("MainScene");         // Return to launcher
}
```

---

### Macro Button System

The `Assets/Scripts/MacroButtons/` directory contains an **input-agnostic macro button system** for the overlay UI. Each button executes a configurable OS-level action (zoom, switch window, etc.) and accepts any form of "click" through a pluggable input layer.

#### Directory Structure

```
Scripts/MacroButtons/
├── Actions/
│   ├── IMacroAction.cs          # Interface all actions implement
│   ├── ZoomInAction.cs          # Sends Ctrl+Plus to OS
│   ├── ZoomOutAction.cs         # Sends Ctrl+Minus to OS
│   ├── SwitchWindowAction.cs    # Sends Alt+Tab to OS
│   └── CalibrationAction.cs     # Placeholder for eye-tracker calibration
├── Input/
│   ├── IInputProvider.cs        # Interface for input detection methods
│   └── PointerInputProvider.cs  # Default: mouse/touch/pen via PointerUpEvent
├── MacroButton.cs               # Custom UI Toolkit element ([UxmlElement])
├── MacroButtonBinding.cs        # Serializable button-name-to-action mapping
├── MacroButtonController.cs     # MonoBehaviour that wires everything together
├── MacroActionType.cs           # Enum of available action types
└── MacroActionFactory.cs        # Creates action instances from enum values
```

#### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     MacroButtonController                      │
│  (MonoBehaviour on UIDocument GameObject)                      │
│                                                                │
│  [SerializeField] List<MacroButtonBinding> bindings            │
│    ┌────────────────┬──────────────────┐                      │
│    │  buttonName     │  actionType      │                     │
│    │  "btn-zoom-in"  │  ZoomIn          │                     │
│    │  "btn-zoom-out" │  ZoomOut         │                     │
│    │  "btn-switch"   │  SwitchWindow    │                     │
│    └────────────────┴──────────────────┘                      │
│                        │                                       │
│    Queries UXML for <MacroButton name="...">                  │
│    Creates IMacroAction via MacroActionFactory                 │
│    Attaches IInputProvider to each button                     │
└──────────────────────────────────────────────────────────────┘
```

#### Setup

1. In `overlay.uxml`, use `<MacroButton>` instead of `<ui:Button>` for any button you want to be a macro:

   ```xml
   <MacroButton name="btn-zoom-in" class="circular-buttons button-look" ... />
   ```

2. On the `UIDocument` GameObject in the scene, add the `MacroButtonController` component.

3. In the Inspector, expand the **Bindings** list and add entries:
   - **Button Name** → the `name` attribute from UXML (e.g. `btn-zoom-in`)
   - **Action Type** → pick from the dropdown (`ZoomIn`, `ZoomOut`, `SwitchWindow`, `Calibration`)

4. Enter Play Mode — check Console for `[MacroButtonController] Bound 'btn-zoom-in' → Zoom In`.

> **Note:** Zoom and switch-window actions send OS-level keystrokes via `keybd_event` P/Invoke. They only work in **standalone builds** (gated behind `#if !UNITY_EDITOR`), consistent with `WindowManager.cs`.

#### Adding a New Action

1. Create a class implementing `IMacroAction`:

   ```csharp
   public class MyNewAction : IMacroAction
   {
       public string ActionId => "my_action";
       public string DisplayName => "My Action";
       public void Execute() { /* your logic */ }
   }
   ```

2. Add an entry to the `MacroActionType` enum in `MacroActionType.cs`.

3. Add a case to `MacroActionFactory.Create()`:

   ```csharp
   MacroActionType.MyAction => new MyNewAction(),
   ```

4. In the Inspector, you can now select `MyAction` from the dropdown.

#### Adding a New Input Provider

To support a new input method (e.g. gaze dwell, voice command):

1. Create a class implementing `IInputProvider`:

   ```csharp
   public class GazeDwellInputProvider : IInputProvider
   {
       public void Attach(VisualElement target, Action onActivated) { /* ... */ }
       public void Detach(VisualElement target) { /* ... */ }
   }
   ```

2. In `MacroButtonController.OnEnable()`, swap the provider:

   ```csharp
   inputProvider = new GazeDwellInputProvider();
   ```

---

### Utility Components

#### `WinShortcut.cs`

**Purpose:** Parses Windows `.lnk` files to extract target path, hotkey, and attributes.

**LNK file structure parsing:**

```csharp
public WinShortcut(string path)
{
    using (var istream = File.OpenRead(path))
    {
        this.Parse(istream);
    }
}

private void Parse(Stream istream)
{
    var linkFlags = this.ParseHeader(istream);
    
    if ((linkFlags & LinkFlags.HasLinkTargetIdList) == LinkFlags.HasLinkTargetIdList)
        this.ParseTargetIDList(istream);
        
    if ((linkFlags & LinkFlags.HasLinkInfo) == LinkFlags.HasLinkInfo)
        this.ParseLinkInfo(istream);
}

private int ParseHeader(Stream stream)
{
    stream.Seek(20, SeekOrigin.Begin);  // Jump to LinkFlags at offset 0x14
    
    var buffer = new byte[4];
    stream.Read(buffer, 0, 4);
    var linkFlags = BitConverter.ToInt32(buffer, 0);

    stream.Read(buffer, 0, 4);
    var fileAttrFlags = BitConverter.ToInt32(buffer, 0);
    IsDirectory = (fileAttrFlags & FileAttributes.Directory) == FileAttributes.Directory;

    stream.Seek(36, SeekOrigin.Current);
    stream.Read(buffer, 0, 2);
    
    return linkFlags;
}

private void ParseLinkInfo(Stream stream)
{
    var start = stream.Position;
    stream.Seek(8, SeekOrigin.Current);
    
    var buffer = new byte[4];
    stream.Read(buffer, 0, 4);
    var lnkInfoFlags = BitConverter.ToInt32(buffer, 0);
    
    if ((lnkInfoFlags & LinkInfoFlags.VolumeIDAndLocalBasePath) != 0)
    {
        stream.Seek(4, SeekOrigin.Current);
        stream.Read(buffer, 0, 4);
        var localBasePathOffset = BitConverter.ToInt32(buffer, 0);
        
        stream.Seek(start + localBasePathOffset, SeekOrigin.Begin);
        
        using (var ms = new MemoryStream())
        {
            int b;
            while ((b = stream.ReadByte()) > 0)
                ms.WriteByte((byte)b);
            TargetPath = Encoding.Default.GetString(ms.ToArray());
        }
    }
}
```

---

#### `Transparency.cs`

**Purpose:** Manages window transparency and click-through state using Windows API polling.

**The Catch-22 Problem:**
When `WS_EX_TRANSPARENT` is set, Windows doesn't send mouse events to Unity — they pass through to whatever is behind. So Unity's `OnPointerEnter`/`OnPointerExit` events never fire, making it impossible to toggle click-through using standard Unity UI events.

**The Solution:**
Use `GetCursorPos()` to poll mouse position directly from Windows every frame (this works even in click-through mode), then raycast against UI elements from Unity's side.

```csharp
public class Transparency : MonoBehaviour
{
    private IntPtr hWnd;
    private bool isClickThrough = true;
    
    // Debounce: 6 frames at 60Hz = 100ms
    private const float TOGGLE_COOLDOWN = 0.1f;
    private float lastToggleTime = 0f;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    void Start()
    {
        #if !UNITY_EDITOR
        hWnd = GetActiveWindow();
        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
        SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, 0);
        #endif
    }

    void Update()
    {
        #if !UNITY_EDITOR
        POINT cursorPos;
        if (!GetCursorPos(out cursorPos))
            return;
        
        POINT clientPos = cursorPos;
        ScreenToClient(hWnd, ref clientPos);
        
        Vector2 unityScreenPos = new Vector2(clientPos.X, Screen.height - clientPos.Y);
        
        bool overUI = IsPointerOverUI(unityScreenPos);
        
        if (Time.time - lastToggleTime < TOGGLE_COOLDOWN)
            return;
        
        if (overUI && isClickThrough)
        {
            SetClickThrough(false);
            lastToggleTime = Time.time;
        }
        else if (!overUI && !isClickThrough)
        {
            SetClickThrough(true);
            lastToggleTime = Time.time;
        }
        #endif
    }
    
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;
        
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        return results.Count > 0;
    }

    public void SetClickThrough(bool clickThrough)
    {
        #if !UNITY_EDITOR
        if (clickThrough)
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        else
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);
            
        isClickThrough = clickThrough;
        #endif
    }
}
```

**How it works:**

1. `GetCursorPos()` gets global mouse position from Windows (works in click-through mode!)
2. `ScreenToClient()` converts to window-relative coordinates
3. `EventSystem.RaycastAll()` checks if cursor is over any UI element
4. Toggle click-through: over UI → disable (buttons work), not over UI → enable (clicks pass through)
5. Debounce prevents rapid toggling at UI edges (6 frames / 100ms cooldown)
