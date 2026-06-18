using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Manages the GazeCalibrationScene:
///   - Launches the Python bridge in unity-calibrate mode
///   - Receives CALI_* UDP messages and drives the calibration dot UI
///   - Returns to the overlay scene once the SVR model is saved
///
/// DOT POSITIONING
///   Python sends normalised [0..1] coords relative to the physical screen.
///   PanelSettings is ConstantPixelSize so logical px == screen px.
///   We position dots at:  left = normX * Screen.width
///                         top  = normY * Screen.height
///   The dot has margin-left = margin-top = -(DOT_SIZE/2) so its visual
///   CENTER sits exactly on that pixel — no offset whatsoever.
///
/// DOT APPEARANCE — SET 100% INLINE (no USS dependency)
///   All visual properties of every dot (live or debug) are set directly
///   in C# via dot.style.*  This ensures they render correctly even if
///   the USS fails to load in a build or the PanelSettings has no stylesheet.
///
/// DEBUG GRID
///   Press G to spawn all 13 numbered red dots simultaneously.
///   Press G again to clear. What you see = what Python targets.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GazeCalibrationController : MonoBehaviour
{
    // ── Dot size (px) — single source of truth for both live and debug dots ──
    private const float DOT_SIZE        = 70f;
    private const float DOT_HALF        = DOT_SIZE / 2f;   // 35 px
    private const float DOT_BORDER      = 3f;
    private const float PROGRESS_H      = 6f;
    private const float PROGRESS_W      = 70f;

    private static readonly Color DOT_COLOR_IDLE       = new Color(0.88f, 0.12f, 0.12f);  // red
    private static readonly Color DOT_COLOR_COLLECTING = new Color(0.12f, 0.42f, 1.00f);  // blue
    private static readonly Color DOT_COLOR_DONE       = new Color(0.05f, 0.76f, 0.30f);  // green
    private static readonly Color DOT_BORDER_COLOR     = Color.white;
    private static readonly Color DEBUG_DOT_COLOR      = new Color(1.00f, 0.20f, 0.20f);

    // ── Phase background colours ──────────────────────────────────────────
    private static readonly Color BG_LIGHT = new Color(0.85f, 0.94f, 0.86f, 1f);
    private static readonly Color BG_DARK  = new Color(0.10f, 0.12f, 0.10f, 1f);

    // ── Glow panel colours (right = warm amber, left = cool blue) ─────────
    private static readonly Color GLOW_RIGHT = new Color(1.00f, 0.72f, 0.10f, 0f);  // starts transparent
    private static readonly Color GLOW_LEFT  = new Color(0.20f, 0.70f, 1.00f, 0f);

    // ── Python-side calibration point list (must match unity_gaze_bridge.py) ─
    private static readonly (float x, float y)[] CalibrationPoints =
    {
        (0.500f, 0.500f),  // 23
        (0.026f, 0.046f),  // 1
        (0.500f, 0.046f),  // 5
        (0.974f, 0.046f),  // 9
        (0.263f, 0.273f),  // 12
        (0.737f, 0.273f),  // 16
        (0.026f, 0.500f),  // 19
        (0.974f, 0.500f),  // 27
        (0.263f, 0.727f),  // 30
        (0.737f, 0.727f),  // 34
        (0.026f, 0.954f),  // 37
        (0.500f, 0.954f),  // 41
        (0.974f, 0.954f),  // 45
        (0.500f, 0.500f),  // 23
    };

    [Tooltip("Scene to load after calibration finishes.")]
    [SerializeField] private string returnSceneName = "OverlayScene";

    [Tooltip("Path to the Python bridge exe, relative to StreamingAssets.")]
    [SerializeField] private string relativeExePath = @"Multimodal_UDP/unity_gaze_bridge/unity_gaze_bridge.exe";

    [Tooltip("Optional explicit camera reference. Leave empty to use Camera.main.")]
    [SerializeField] private Camera sceneCamera;

    // ── UI references ────────────────────────────────────────────────────────
    private VisualElement calibrationRoot;
    private VisualElement dot;
    private VisualElement progressFill;
    private Label         statusLabel;
    private Label         pointLabel;
    private VisualElement fittingOverlay;
    private VisualElement promptCard;
    private VisualElement glowRight;   // right-edge glow panel
    private VisualElement glowLeft;    // left-edge glow panel

    // ── Live dot animation state ───────────────────────────────────────────
    private float dotTargetX;
    private float dotTargetY;
    private float dotCurrentX;
    private float dotCurrentY;
    private bool  dotVisible;
    private bool  firstPointReceived;

    // ── Debug grid ────────────────────────────────────────────────────────
    private bool debugGridVisible;
    private readonly List<VisualElement> debugDots = new List<VisualElement>();

    // ── Calibration results ──────────────────────────────────────────────
    private struct CalibrationPointResult
    {
        public Vector2 actual;
        public Vector2 predicted;
        public float   error;
    }
    private readonly List<CalibrationPointResult> calibrationResults = new List<CalibrationPointResult>();
    private float meanCalibrationError;
    private VisualElement resultsOverlay;

    // Results overlay colours
    private static readonly Color RESULT_TARGET_COLOR    = new Color(0.05f, 0.76f, 0.30f);   // green
    private static readonly Color RESULT_ESTIMATED_COLOR = new Color(1.00f, 0.45f, 0.15f);   // orange
    private static readonly Color RESULT_LINE_COLOR      = new Color(1f, 1f, 1f, 0.5f);      // white 50%
    private const float RESULT_TARGET_SIZE   = 30f;
    private const float RESULT_ESTIMATED_SIZE = 24f;

    // ── Singleton ────────────────────────────────────────────────────────
    public static GazeCalibrationController Instance { get; private set; }

    // ── Process / network ────────────────────────────────────────────────
    private Process            calibrationProcess;
    private UdpClient          udpClient;
    private CancellationTokenSource udpCancellation;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    private string calibrationProfileId;

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;

        if (sceneCamera == null) sceneCamera = Camera.main;
        if (sceneCamera != null)
        {
            sceneCamera.clearFlags     = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.85f, 0.94f, 0.86f, 1f);
        }

#if !UNITY_EDITOR
        WindowManager.MakeFullscreen();
        WindowManager.SetClickThrough(false);
#endif
    }

    private void Start()
    {
        var uiDoc = GetComponent<UIDocument>();
        var root  = uiDoc.rootVisualElement;

        // ── Safety: ensure the stylesheet is always loaded ────────────────
        // The UXML has a <Style> tag for builds; this catches any edge case
        // where the USS still isn't applied (e.g. fresh PanelSettings asset).
#if UNITY_EDITOR
        var uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/GazeCalibration.uss");
        if (uss != null && !root.styleSheets.Contains(uss))
            root.styleSheets.Add(uss);
#endif

        calibrationRoot = root.Q<VisualElement>("calibration-root");
        dot             = root.Q<VisualElement>("calibration-dot");
        progressFill    = root.Q<VisualElement>("progress-fill");
        statusLabel     = root.Q<Label>("status-label");
        pointLabel      = root.Q<Label>("point-label");
        fittingOverlay  = root.Q<VisualElement>("fitting-overlay");
        promptCard      = root.Q<VisualElement>("prompt-card");

        if (fittingOverlay != null)
            fittingOverlay.style.display = DisplayStyle.None;

        // ── Apply inline styles to live dot (immune to CSS loading) ──────
        ApplyDotInlineStyle(dot, DOT_COLOR_IDLE);
        if (dot != null) dot.style.opacity = 0;

        // ── Build glow panels (right and left edge) ───────────────────────
        glowRight = new VisualElement();
        glowRight.style.position    = Position.Absolute;
        glowRight.style.right       = 0;
        glowRight.style.top         = 0;
        glowRight.style.bottom      = 0;
        glowRight.style.width       = 320f;
        glowRight.style.backgroundColor = GLOW_RIGHT;
        glowRight.style.opacity     = 0;
        glowRight.pickingMode       = PickingMode.Ignore;
        calibrationRoot?.Add(glowRight);

        glowLeft = new VisualElement();
        glowLeft.style.position     = Position.Absolute;
        glowLeft.style.left         = 0;
        glowLeft.style.top          = 0;
        glowLeft.style.bottom       = 0;
        glowLeft.style.width        = 320f;
        glowLeft.style.backgroundColor = GLOW_LEFT;
        glowLeft.style.opacity      = 0;
        glowLeft.pickingMode        = PickingMode.Ignore;
        calibrationRoot?.Add(glowLeft);

        GazeFollowerRunner.Instance?.OnCalibrationSceneReady(this);
    }

    // ── Coordinate helpers ────────────────────────────────────────────────
    // Use resolvedStyle to properly support UI scaling (ScaleWithScreenSize).
    private float NX(float n) => n * (calibrationRoot != null && !float.IsNaN(calibrationRoot.resolvedStyle.width) && calibrationRoot.resolvedStyle.width > 0 ? calibrationRoot.resolvedStyle.width : Screen.width);
    private float NY(float n) => n * (calibrationRoot != null && !float.IsNaN(calibrationRoot.resolvedStyle.height) && calibrationRoot.resolvedStyle.height > 0 ? calibrationRoot.resolvedStyle.height : Screen.height);

    // ── Apply all dot visual properties inline (no USS class needed) ──────
    private static void ApplyDotInlineStyle(VisualElement ve, Color bgColor)
    {
        if (ve == null) return;
        ve.style.position          = Position.Absolute;
        ve.style.width             = DOT_SIZE;
        ve.style.height            = DOT_SIZE;
        ve.style.marginLeft        = -DOT_HALF;
        ve.style.marginTop         = -DOT_HALF;
        ve.style.borderTopLeftRadius     = DOT_HALF;
        ve.style.borderTopRightRadius    = DOT_HALF;
        ve.style.borderBottomLeftRadius  = DOT_HALF;
        ve.style.borderBottomRightRadius = DOT_HALF;
        ve.style.backgroundColor   = bgColor;
        ve.style.borderLeftColor   = DOT_BORDER_COLOR;
        ve.style.borderRightColor  = DOT_BORDER_COLOR;
        ve.style.borderTopColor    = DOT_BORDER_COLOR;
        ve.style.borderBottomColor = DOT_BORDER_COLOR;
        ve.style.borderLeftWidth   = DOT_BORDER;
        ve.style.borderRightWidth  = DOT_BORDER;
        ve.style.borderTopWidth    = DOT_BORDER;
        ve.style.borderBottomWidth = DOT_BORDER;

        // Centered focus point inside the dot
        VisualElement focusPoint = ve.Q<VisualElement>("focus-point");
        if (focusPoint == null)
        {
            focusPoint = new VisualElement();
            focusPoint.name = "focus-point";
            ve.Add(focusPoint);
        }

        const float FOCUS_SIZE = 22f; // ~31% of 70px dot size
        const float FOCUS_HALF = FOCUS_SIZE / 2f;

        focusPoint.style.position          = Position.Absolute;
        focusPoint.style.width             = FOCUS_SIZE;
        focusPoint.style.height            = FOCUS_SIZE;
        focusPoint.style.left              = Length.Percent(50);
        focusPoint.style.top               = Length.Percent(50);
        focusPoint.style.marginLeft        = -FOCUS_HALF;
        focusPoint.style.marginTop         = -FOCUS_HALF;
        focusPoint.style.borderTopLeftRadius     = FOCUS_HALF;
        focusPoint.style.borderTopRightRadius    = FOCUS_HALF;
        focusPoint.style.borderBottomLeftRadius  = FOCUS_HALF;
        focusPoint.style.borderBottomRightRadius = FOCUS_HALF;
        focusPoint.style.backgroundColor   = Color.white;
    }

    // ── Start calibration (called by GazeFollowerRunner after camera check) ──

    public void StartCalibration(string profileId = null)
    {
        calibrationProfileId = profileId;
        if (string.IsNullOrEmpty(calibrationProfileId))
        {
            calibrationProfileId = PlayerPrefs.GetString("ActiveProfileID", null);
        }
        StartCoroutine(LaunchBridge());
    }

    private void Update()
    {
        // ── Lerp live dot to target position ────────────────────────────
        if (dotVisible && dot != null)
        {
            dotCurrentX = Mathf.Lerp(dotCurrentX, dotTargetX, Time.deltaTime * 8f);
            dotCurrentY = Mathf.Lerp(dotCurrentY, dotTargetY, Time.deltaTime * 8f);
            dot.style.left = NX(dotCurrentX);
            dot.style.top  = NY(dotCurrentY);
        }

        // ── Drain UDP messages (must happen on main thread) ───────────
        while (messageQueue.TryDequeue(out string msg))
            HandleMessage(msg);

        // ── Debug grid toggle (G key) ─────────────────────────────────
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            ToggleDebugGrid();
    }

    // ── Bridge launch ─────────────────────────────────────────────────────

    private IEnumerator LaunchBridge()
    {
        string exePath = Path.Combine(Application.streamingAssetsPath, relativeExePath)
                             .Replace("/", "\\");
        if (!File.Exists(exePath))
        {
            SetStatus($"Bridge executable not found:\n{exePath}");
            yield break;
        }

        udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port  = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;

        udpCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoop(udpCancellation.Token));

        var startInfo = new ProcessStartInfo
        {
            FileName         = exePath,
            Arguments        = $"--port {port} --mode unity-calibrate"
                               + (string.IsNullOrEmpty(calibrationProfileId) ? ""
                                  : $" --profile-id \"{calibrationProfileId}\""),
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        try
        {
            calibrationProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            calibrationProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    RobitLogger.Log($"[GazeCalib][PY] {e.Data}");
            };
            calibrationProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    if (e.Data.Contains("INFO:"))
                        RobitLogger.Log($"[GazeCalib][PY-INFO] {e.Data}");
                    else if (e.Data.Contains("WARNING:"))
                        RobitLogger.LogWarning($"[GazeCalib][PY-WARN] {e.Data}");
                    else
                        RobitLogger.LogError($"[GazeCalib][PY-ERR] {e.Data}");
                }
            };
            calibrationProcess.Start();
            ChildProcessTracker.AddProcess(calibrationProcess);
            calibrationProcess.BeginOutputReadLine();
            calibrationProcess.BeginErrorReadLine();
            SetStatus("Initializing gaze model…\nThis may take a few seconds.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to start calibration bridge:\n{ex.Message}");
        }
    }

    // ── UDP receive loop (background thread) ────────────────────────────

    private async Task ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync();
                messageQueue.Enqueue(Encoding.UTF8.GetString(result.Buffer).Trim());
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                RobitLogger.LogWarning($"[GazeCalib] UDP error: {ex.Message}");
            }
        }
    }

    // ── Message handling (main thread via Update) ───────────────────────

    private void HandleMessage(string msg)
    {
        // ── CALI_SHOW_POINT {x},{y} {idx} {total} ─────────────────────
        if (msg.StartsWith("CALI_SHOW_POINT ", StringComparison.Ordinal))
        {
            string payload = msg.Substring("CALI_SHOW_POINT ".Length);
            string[] parts = payload.Split(' ');
            if (parts.Length >= 3)
            {
                string[] xy = parts[0].Split(',');
                if (xy.Length == 2
                    && float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    if (!firstPointReceived)
                    {
                        dotCurrentX = x;
                        dotCurrentY = y;
                        if (dot != null)
                        {
                            dot.style.left = NX(x);
                            dot.style.top  = NY(y);
                        }
                        firstPointReceived = true;
                    }

                    dotTargetX = x;
                    dotTargetY = y;
                    dotVisible = true;

                    if (dot != null)
                    {
                        dot.style.opacity = 1;
                        ApplyDotInlineStyle(dot, DOT_COLOR_IDLE);
                    }

                    if (progressFill != null)
                        progressFill.style.width = Length.Percent(0);
                }

                if (pointLabel != null)
                    pointLabel.text = $"Point  {parts[1]}  /  {parts[2]}";

                SetStatus("Look at the dot and hold still");
            }
        }

        // ── CALI_PROGRESS {0-100} ───────────────────────────────────────
        else if (msg.StartsWith("CALI_PROGRESS ", StringComparison.Ordinal))
        {
            if (int.TryParse(msg.Substring("CALI_PROGRESS ".Length).Trim(), out int pct))
            {
                if (progressFill != null)
                    progressFill.style.width = Length.Percent(pct);

                if (dot != null && pct > 0)
                    ApplyDotInlineStyle(dot, DOT_COLOR_COLLECTING);

                SetStatus($"Collecting… {pct}%");
            }
        }

        // ── CALI_POINT_DONE {idx} {total} ──────────────────────────────
        else if (msg.StartsWith("CALI_POINT_DONE", StringComparison.Ordinal))
        {
            if (dot != null)
                ApplyDotInlineStyle(dot, DOT_COLOR_DONE);

            if (progressFill != null)
                progressFill.style.width = Length.Percent(100);

            // Parse current point index for feedback
            string[] parts = msg.Split(' ');
            string idx   = parts.Length > 1 ? parts[1] : "?";
            string total = parts.Length > 2 ? parts[2] : "?";
            SetStatus($"✓ Point {idx} of {total} done!");

            if (pointLabel != null)
                pointLabel.text = $"Point  {idx}  /  {total}  ✓";
        }

        // ── CALI_MODEL_FITTING ─────────────────────────────────────────
        else if (msg == "CALI_MODEL_FITTING")
        {
            if (dot != null) dot.style.opacity = 0;
            if (fittingOverlay != null)
                fittingOverlay.style.display = DisplayStyle.Flex;
            SetStatus("Fitting gaze model… please wait");
        }

        // ── CALI_MODEL_READY {mean_error} ──────────────────────────────
        else if (msg.StartsWith("CALI_MODEL_READY", StringComparison.Ordinal))
        {
            string errPart = msg.Substring("CALI_MODEL_READY".Length).Trim();
            if (float.TryParse(errPart, NumberStyles.Float, CultureInfo.InvariantCulture, out float err))
            {
                meanCalibrationError = err;
                SetStatus($"✓ Calibration complete!\nAvg error: {err:F4}");
            }
            else
            {
                SetStatus("✓ Calibration complete!");
            }

            if (pointLabel != null)
                pointLabel.text = "All points collected";

            // Mark the profile as calibrated in profiles.json now that the
            // SVR model files have been confirmed saved on the Python side.
            if (!string.IsNullOrEmpty(calibrationProfileId))
            {
                var data    = ProfileManager.LoadProfiles();
                var profile = data.profiles.Find(p => p.id == calibrationProfileId);
                if (profile != null)
                {
                    profile.isCalibrated        = true;
                    profile.lastCalibrationDate = DateTime.Now.ToString("dd/MM/yy HH:mm");
                    ProfileManager.SaveProfiles(data);
                    RobitLogger.Log($"[GazeCalib] Profile '{profile.name}' marked as calibrated.");
                }
            }

            // Don't auto-return — wait for CALI_RESULTS_DONE to show accuracy screen.
            calibrationResults.Clear();
        }

        // ── CALI_POINT_RESULT {actual_x},{actual_y} {pred_x},{pred_y} {error} ──
        else if (msg.StartsWith("CALI_POINT_RESULT ", StringComparison.Ordinal))
        {
            string payload = msg.Substring("CALI_POINT_RESULT ".Length);
            string[] parts = payload.Split(' ');
            if (parts.Length >= 3)
            {
                string[] actualXY = parts[0].Split(',');
                string[] predXY   = parts[1].Split(',');
                if (actualXY.Length == 2 && predXY.Length == 2
                    && float.TryParse(actualXY[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax)
                    && float.TryParse(actualXY[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay)
                    && float.TryParse(predXY[0],   NumberStyles.Float, CultureInfo.InvariantCulture, out float px)
                    && float.TryParse(predXY[1],   NumberStyles.Float, CultureInfo.InvariantCulture, out float py)
                    && float.TryParse(parts[2],    NumberStyles.Float, CultureInfo.InvariantCulture, out float ptErr))
                {
                    calibrationResults.Add(new CalibrationPointResult
                    {
                        actual    = new Vector2(ax, ay),
                        predicted = new Vector2(px, py),
                        error     = ptErr
                    });
                }
            }
        }

        // ── CALI_RESULTS_DONE — show accuracy overlay ──────────────────
        else if (msg == "CALI_RESULTS_DONE")
        {
            if (fittingOverlay != null)
                fittingOverlay.style.display = DisplayStyle.None;

            BuildResultsOverlay();
        }

        // ── CALI_MODEL_ERROR {reason} ──────────────────────────────────
        else if (msg.StartsWith("CALI_MODEL_ERROR", StringComparison.Ordinal))
        {
            if (fittingOverlay != null)
                fittingOverlay.style.display = DisplayStyle.None;
            SetStatus($"⚠ Calibration failed:\n{msg.Substring("CALI_MODEL_ERROR".Length).Trim()}\n\nRestart to retry.");
        }

        // ── CALI_START {total} ─────────────────────────────────────────
        else if (msg.StartsWith("CALI_START", StringComparison.Ordinal))
        {
            SetStatus("Calibration starting…\nFocus on each dot as it appears.");
        }

        // ── CALI_PAUSED ────────────────────────────────────────────────
        // Camera feed stalled — hide dot, warn user, tint progress bar amber.
        else if (msg == "CALI_PAUSED")
        {
            if (dot != null) dot.style.opacity = 0;

            if (progressFill != null)
                progressFill.style.backgroundColor = new Color(1.00f, 0.72f, 0.10f); // amber

            SetStatus("⚠ Camera feed lost.\nPlease check your connection — calibration will resume automatically.");
        }

        // ── CALI_RESUMED ───────────────────────────────────────────────
        // Camera feed recovered — restore dot and progress bar colour.
        else if (msg == "CALI_RESUMED")
        {
            if (dot != null) dot.style.opacity = 1;

            if (progressFill != null)
                progressFill.style.backgroundColor = new Color(0.12f, 0.42f, 1.00f); // collecting blue

            SetStatus("Camera reconnected — resuming calibration…");
        }

        // ── CALI_PHASE {phase_id} ──────────────────────────────────────
        else if (msg.StartsWith("CALI_PHASE ", StringComparison.Ordinal))
        {
            string phase = msg.Substring("CALI_PHASE ".Length).Trim();
            HandlePhaseTransition(phase);
        }
    }

    // ── Phase transition ──────────────────────────────────────────────────

    private void HandlePhaseTransition(string phase)
    {
        if (glowRight != null) glowRight.style.opacity = 0;
        if (glowLeft  != null) glowLeft.style.opacity  = 0;

        switch (phase)
        {
            case "PHASE_LIGHT":
                SetCameraBackground(BG_LIGHT);
                SetLabelColors(new Color(0.20f, 0.27f, 0.18f));
                SetPromptCardColors(new Color(1f, 1f, 1f, 0.78f), new Color(0.31f, 0.63f, 0.39f, 0.78f));
                SetStatus("Follow the dot as it appears.");
                firstPointReceived = false;
                break;
            case "PHASE_DARK":
                SetCameraBackground(BG_DARK);
                SetLabelColors(new Color(0.85f, 0.94f, 0.86f));
                SetPromptCardColors(new Color(0f, 0f, 0f, 0.78f), new Color(0.31f, 0.63f, 0.39f, 0.78f));
                SetStatus("Follow the dot as it appears.");
                firstPointReceived = false;
                break;
            case "PHASE_RIGHT_TILT":
                SetCameraBackground(BG_LIGHT);
                SetLabelColors(new Color(0.20f, 0.27f, 0.18f));
                SetPromptCardColors(new Color(1f, 1f, 1f, 0.78f), new Color(0.31f, 0.63f, 0.39f, 0.78f));
                SetStatus("Tilt your head to the RIGHT.");
                firstPointReceived = false;
                if (glowRight != null) StartCoroutine(GlowAndFade(glowRight, GLOW_RIGHT, 4, 1.0f));
                break;
            case "PHASE_LEFT_TILT":
                SetCameraBackground(BG_LIGHT);
                SetLabelColors(new Color(0.20f, 0.27f, 0.18f));
                SetPromptCardColors(new Color(1f, 1f, 1f, 0.78f), new Color(0.31f, 0.63f, 0.39f, 0.78f));
                SetStatus("Tilt your head to the LEFT.");
                firstPointReceived = false;
                if (glowLeft != null) StartCoroutine(GlowAndFade(glowLeft, GLOW_LEFT, 4, 1.0f));
                break;
        }
    }

    private void SetCameraBackground(Color color)
    {
        if (sceneCamera != null)
            sceneCamera.backgroundColor = color;
            
        if (calibrationRoot != null)
            calibrationRoot.style.backgroundColor = color;
    }

    private void SetLabelColors(Color color)
    {
        if (statusLabel != null) statusLabel.style.color = color;
        if (pointLabel  != null) pointLabel.style.color  = color;
    }

    private void SetPromptCardColors(Color bgColor, Color borderColor)
    {
        if (promptCard != null)
        {
            promptCard.style.backgroundColor = bgColor;
            promptCard.style.borderLeftColor = borderColor;
            promptCard.style.borderRightColor = borderColor;
            promptCard.style.borderTopColor = borderColor;
            promptCard.style.borderBottomColor = borderColor;
        }
    }

    // ── Glow animation coroutine ──────────────────────────────────────────
    // Pulses the panel opacity from 0 → peak → 0, repeated `pulses` times.
    // Each pulse takes `pulseDuration` seconds.

    private System.Collections.IEnumerator GlowAndFade(VisualElement panel, Color baseColor, int pulses, float pulseDuration)
    {
        if (panel == null) yield break;
        panel.style.backgroundColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

        for (int i = 0; i < pulses; i++)
        {
            // Fade in
            float elapsed = 0f;
            float half = pulseDuration * 0.5f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                panel.style.opacity = Mathf.Clamp01(elapsed / half);
                yield return null;
            }
            panel.style.opacity = 1f;

            // Fade out
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                panel.style.opacity = Mathf.Clamp01(1f - elapsed / half);
                yield return null;
            }
            panel.style.opacity = 0f;
        }

        // Leave panel fully invisible after animation completes
        panel.style.opacity = 0;
    }

    // ── Debug Grid ────────────────────────────────────────────────────────
    // Spawns ALL 23 calibration points simultaneously as numbered red circles.
    // Each dot uses IDENTICAL positioning and styling to the live calibration dot.
    // What you see in the debug grid = exactly where Python will place each point.

    private void ToggleDebugGrid()
    {
        if (debugGridVisible) ClearDebugGrid();
        else                  ShowDebugGrid();
        debugGridVisible = !debugGridVisible;
    }

    private void ShowDebugGrid()
    {
        if (calibrationRoot == null)
        {
            RobitLogger.LogWarning("[GazeCalibrationController] calibrationRoot is null.");
            return;
        }
        ClearDebugGrid();

        for (int i = 0; i < CalibrationPoints.Length; i++)
        {
            (float x, float y) = CalibrationPoints[i];

            // Outer circle — SAME inline styling as live dot
            var d = new VisualElement();
            ApplyDotInlineStyle(d, DEBUG_DOT_COLOR);
            d.style.left    = NX(x);
            d.style.top     = NY(y);
            d.style.opacity = 0.9f;

            // White index number centered inside the focus point for contrast
            var lbl = new Label((i + 1).ToString());
            lbl.style.position        = Position.Absolute;
            lbl.style.left            = 0; lbl.style.right = 0;
            lbl.style.top             = 0; lbl.style.bottom = 0;
            lbl.style.unityTextAlign  = TextAnchor.MiddleCenter;
            lbl.style.color           = new Color(0.12f, 0.12f, 0.12f); // Dark gray/black for readability on white focus point
            lbl.style.fontSize        = 13;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;

            var fp = d.Q<VisualElement>("focus-point");
            if (fp != null)
            {
                fp.Add(lbl);
            }
            else
            {
                lbl.style.color = Color.white; // Fallback if focus point is missing
                d.Add(lbl);
            }

            calibrationRoot.Add(d);
            debugDots.Add(d);
        }

        RobitLogger.Log($"[GazeCalib] Debug grid: {CalibrationPoints.Length} dots shown. Press G to clear.");
    }

    private void ClearDebugGrid()
    {
        foreach (var d in debugDots) d.RemoveFromHierarchy();
        debugDots.Clear();
    }

    // ── Results overlay ───────────────────────────────────────────────────

    private void BuildResultsOverlay()
    {
        if (calibrationRoot == null) return;

        // Remove any previous results overlay
        resultsOverlay?.RemoveFromHierarchy();

        resultsOverlay = new VisualElement();
        resultsOverlay.style.position        = Position.Absolute;
        resultsOverlay.style.left            = 0;
        resultsOverlay.style.top             = 0;
        resultsOverlay.style.right           = 0;
        resultsOverlay.style.bottom          = 0;
        resultsOverlay.style.backgroundColor = new Color(0.08f, 0.10f, 0.08f, 0.92f);

        // ── Draw per-point results (lines, target dots, estimated dots) ──
        foreach (var result in calibrationResults)
        {
            float ax = NX(result.actual.x);
            float ay = NY(result.actual.y);
            float px = NX(result.predicted.x);
            float py = NY(result.predicted.y);

            // Error line: thin rotated element connecting actual → predicted
            float dx = px - ax;
            float dy = py - ay;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float angle  = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

            if (length > 1f)  // only draw line if there's visible error
            {
                var line = new VisualElement();
                line.style.position        = Position.Absolute;
                line.style.left            = ax;
                line.style.top             = ay - 1f;
                line.style.width           = length;
                line.style.height          = 2f;
                line.style.backgroundColor = RESULT_LINE_COLOR;
                line.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
                line.style.rotate          = new Rotate(Angle.Degrees(angle));
                line.pickingMode           = PickingMode.Ignore;
                resultsOverlay.Add(line);
            }

            // Target dot (green)
            var targetDot = new VisualElement();
            targetDot.style.position                 = Position.Absolute;
            targetDot.style.width                    = RESULT_TARGET_SIZE;
            targetDot.style.height                   = RESULT_TARGET_SIZE;
            targetDot.style.left                     = ax;
            targetDot.style.top                      = ay;
            targetDot.style.marginLeft                = -RESULT_TARGET_SIZE / 2f;
            targetDot.style.marginTop                 = -RESULT_TARGET_SIZE / 2f;
            targetDot.style.borderTopLeftRadius       = RESULT_TARGET_SIZE / 2f;
            targetDot.style.borderTopRightRadius      = RESULT_TARGET_SIZE / 2f;
            targetDot.style.borderBottomLeftRadius    = RESULT_TARGET_SIZE / 2f;
            targetDot.style.borderBottomRightRadius   = RESULT_TARGET_SIZE / 2f;
            targetDot.style.backgroundColor           = RESULT_TARGET_COLOR;
            targetDot.style.borderLeftWidth            = 2f;
            targetDot.style.borderRightWidth           = 2f;
            targetDot.style.borderTopWidth             = 2f;
            targetDot.style.borderBottomWidth          = 2f;
            targetDot.style.borderLeftColor            = Color.white;
            targetDot.style.borderRightColor           = Color.white;
            targetDot.style.borderTopColor             = Color.white;
            targetDot.style.borderBottomColor          = Color.white;
            targetDot.pickingMode                      = PickingMode.Ignore;
            resultsOverlay.Add(targetDot);

            // Estimated dot (orange)
            var estDot = new VisualElement();
            estDot.style.position                 = Position.Absolute;
            estDot.style.width                    = RESULT_ESTIMATED_SIZE;
            estDot.style.height                   = RESULT_ESTIMATED_SIZE;
            estDot.style.left                     = px;
            estDot.style.top                      = py;
            estDot.style.marginLeft                = -RESULT_ESTIMATED_SIZE / 2f;
            estDot.style.marginTop                 = -RESULT_ESTIMATED_SIZE / 2f;
            estDot.style.borderTopLeftRadius       = RESULT_ESTIMATED_SIZE / 2f;
            estDot.style.borderTopRightRadius      = RESULT_ESTIMATED_SIZE / 2f;
            estDot.style.borderBottomLeftRadius    = RESULT_ESTIMATED_SIZE / 2f;
            estDot.style.borderBottomRightRadius   = RESULT_ESTIMATED_SIZE / 2f;
            estDot.style.backgroundColor           = RESULT_ESTIMATED_COLOR;
            estDot.style.borderLeftWidth            = 2f;
            estDot.style.borderRightWidth           = 2f;
            estDot.style.borderTopWidth             = 2f;
            estDot.style.borderBottomWidth          = 2f;
            estDot.style.borderLeftColor            = new Color(1f, 1f, 1f, 0.7f);
            estDot.style.borderRightColor           = new Color(1f, 1f, 1f, 0.7f);
            estDot.style.borderTopColor             = new Color(1f, 1f, 1f, 0.7f);
            estDot.style.borderBottomColor          = new Color(1f, 1f, 1f, 0.7f);
            estDot.pickingMode                      = PickingMode.Ignore;
            resultsOverlay.Add(estDot);
        }

        // ── Summary card (centred) ────────────────────────────────────────
        var cardContainer = new VisualElement();
        cardContainer.style.position       = Position.Absolute;
        cardContainer.style.left           = 0;
        cardContainer.style.right          = 0;
        cardContainer.style.top            = 0;
        cardContainer.style.bottom         = 0;
        cardContainer.style.alignItems     = Align.Center;
        cardContainer.style.justifyContent = Justify.Center;
        cardContainer.pickingMode          = PickingMode.Ignore;

        var card = new VisualElement();
        card.style.backgroundColor           = new Color(0.12f, 0.14f, 0.12f, 0.95f);
        card.style.borderTopLeftRadius       = 20f;
        card.style.borderTopRightRadius      = 20f;
        card.style.borderBottomLeftRadius    = 20f;
        card.style.borderBottomRightRadius   = 20f;
        card.style.borderLeftWidth            = 2f;
        card.style.borderRightWidth           = 2f;
        card.style.borderTopWidth             = 2f;
        card.style.borderBottomWidth          = 2f;
        card.style.borderLeftColor            = new Color(0.3f, 0.7f, 0.4f, 0.6f);
        card.style.borderRightColor           = new Color(0.3f, 0.7f, 0.4f, 0.6f);
        card.style.borderTopColor             = new Color(0.3f, 0.7f, 0.4f, 0.6f);
        card.style.borderBottomColor          = new Color(0.3f, 0.7f, 0.4f, 0.6f);
        card.style.paddingLeft                = 48f;
        card.style.paddingRight               = 48f;
        card.style.paddingTop                 = 36f;
        card.style.paddingBottom              = 36f;
        card.style.alignItems                 = Align.Center;
        card.style.minWidth                   = 400f;

        // Title
        var title = new Label("Calibration Complete");
        title.style.color          = Color.white;
        title.style.fontSize       = 28;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom   = 16f;
        card.Add(title);

        // Error value
        var errorLabel = new Label($"Mean Error: {meanCalibrationError:F4}");
        errorLabel.style.color          = new Color(0.75f, 0.75f, 0.75f);
        errorLabel.style.fontSize       = 18;
        errorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        errorLabel.style.marginBottom   = 8f;
        card.Add(errorLabel);

        // Accuracy rating
        string ratingText;
        Color  ratingColor;
        GetAccuracyRating(meanCalibrationError, out ratingText, out ratingColor);

        var ratingLabel = new Label(ratingText);
        ratingLabel.style.color          = ratingColor;
        ratingLabel.style.fontSize       = 22;
        ratingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        ratingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        ratingLabel.style.marginBottom   = 8f;
        card.Add(ratingLabel);

        // Points count
        var pointsLabel = new Label($"{calibrationResults.Count} calibration points evaluated");
        pointsLabel.style.color          = new Color(0.55f, 0.55f, 0.55f);
        pointsLabel.style.fontSize       = 14;
        pointsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        pointsLabel.style.marginBottom   = 24f;
        card.Add(pointsLabel);

        // Legend
        var legend = new VisualElement();
        legend.style.flexDirection = FlexDirection.Row;
        legend.style.justifyContent = Justify.Center;
        legend.style.marginBottom = 24f;

        legend.Add(CreateLegendItem(RESULT_TARGET_COLOR, "Target"));
        var spacer = new VisualElement();
        spacer.style.width = 30f;
        legend.Add(spacer);
        legend.Add(CreateLegendItem(RESULT_ESTIMATED_COLOR, "Estimated"));
        card.Add(legend);

        // Buttons row
        var buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.justifyContent = Justify.Center;

        var recalibrateBtn = CreateResultButton("Recalibrate",
            new Color(0.90f, 0.55f, 0.15f), new Color(0.70f, 0.40f, 0.10f));
        recalibrateBtn.clicked += OnRecalibrateClicked;
        buttonsRow.Add(recalibrateBtn);

        var btnSpacer = new VisualElement();
        btnSpacer.style.width = 20f;
        buttonsRow.Add(btnSpacer);

        var continueBtn = CreateResultButton("Continue",
            new Color(0.15f, 0.65f, 0.35f), new Color(0.10f, 0.50f, 0.25f));
        continueBtn.clicked += OnContinueClicked;
        buttonsRow.Add(continueBtn);

        card.Add(buttonsRow);
        cardContainer.Add(card);
        resultsOverlay.Add(cardContainer);
        calibrationRoot.Add(resultsOverlay);
    }

    private static void GetAccuracyRating(float error, out string text, out Color color)
    {
        if (error < 0.015f)
        {
            text  = "★ Excellent";
            color = new Color(0.10f, 0.85f, 0.35f);
        }
        else if (error < 0.030f)
        {
            text  = "● Good";
            color = new Color(0.20f, 0.60f, 1.00f);
        }
        else if (error < 0.050f)
        {
            text  = "◆ Fair";
            color = new Color(1.00f, 0.72f, 0.10f);
        }
        else
        {
            text  = "▲ Poor";
            color = new Color(0.95f, 0.25f, 0.20f);
        }
    }

    private VisualElement CreateLegendItem(Color dotColor, string labelText)
    {
        var item = new VisualElement();
        item.style.flexDirection = FlexDirection.Row;
        item.style.alignItems   = Align.Center;

        var swatch = new VisualElement();
        swatch.style.width                    = 14f;
        swatch.style.height                   = 14f;
        swatch.style.borderTopLeftRadius      = 7f;
        swatch.style.borderTopRightRadius     = 7f;
        swatch.style.borderBottomLeftRadius   = 7f;
        swatch.style.borderBottomRightRadius  = 7f;
        swatch.style.backgroundColor          = dotColor;
        swatch.style.marginRight              = 6f;
        item.Add(swatch);

        var lbl = new Label(labelText);
        lbl.style.color    = new Color(0.75f, 0.75f, 0.75f);
        lbl.style.fontSize = 14;
        item.Add(lbl);

        return item;
    }

    private Button CreateResultButton(string text, Color bgColor, Color hoverColor)
    {
        var btn = new Button();
        btn.text = text;
        btn.style.backgroundColor = bgColor;
        btn.style.color           = Color.white;
        btn.style.fontSize        = 18;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.paddingLeft     = 32f;
        btn.style.paddingRight    = 32f;
        btn.style.paddingTop      = 14f;
        btn.style.paddingBottom   = 14f;
        btn.style.borderTopLeftRadius     = 12f;
        btn.style.borderTopRightRadius    = 12f;
        btn.style.borderBottomLeftRadius  = 12f;
        btn.style.borderBottomRightRadius = 12f;
        btn.style.borderLeftWidth   = 0;
        btn.style.borderRightWidth  = 0;
        btn.style.borderTopWidth    = 0;
        btn.style.borderBottomWidth = 0;

        btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = hoverColor);
        btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = bgColor);

        return btn;
    }

    private void OnContinueClicked()
    {
        resultsOverlay?.RemoveFromHierarchy();
        resultsOverlay = null;
        StartCoroutine(FinishAndReturn(0f));
    }

    private void OnRecalibrateClicked()
    {
        resultsOverlay?.RemoveFromHierarchy();
        resultsOverlay = null;
        Cleanup();
        calibrationResults.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ── Finish ────────────────────────────────────────────────────────────

    private IEnumerator FinishAndReturn(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Cleanup();
        GazeFollowerRunner.Instance?.NotifyCalibrationComplete();
        SceneManager.LoadScene(returnSceneName);
    }

    private void SetStatus(string text)
    {
        if (statusLabel != null) statusLabel.text = text;
    }

    private void Cleanup()
    {
        resultsOverlay?.RemoveFromHierarchy();
        resultsOverlay = null;
        ClearDebugGrid();
        udpCancellation?.Cancel();
        udpCancellation?.Dispose();
        udpCancellation = null;
        udpClient?.Close();
        udpClient?.Dispose();
        udpClient = null;
        try { if (calibrationProcess != null && !calibrationProcess.HasExited) calibrationProcess.Kill(); }
        catch { }
        calibrationProcess?.Dispose();
        calibrationProcess = null;
    }

    private void OnDestroy()    { if (Instance == this) Instance = null; Cleanup(); }
    private void OnApplicationQuit() => Cleanup();
}

