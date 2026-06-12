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
    private const float DOT_SIZE        = 45f;
    private const float DOT_HALF        = DOT_SIZE / 2f;   // 22.5 px
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
        (0.500f, 0.500f),  // 23 center (start)
        (0.026f, 0.046f),  //  1 top-left
        (0.263f, 0.046f),  //  3 top-inner-left
        (0.500f, 0.046f),  //  5 top-center
        (0.737f, 0.046f),  //  7 top-inner-right
        (0.974f, 0.046f),  //  9 top-right
        (0.026f, 0.273f),  // 10 row 2 left
        (0.263f, 0.273f),  // 12 row 2 inner-left
        (0.737f, 0.273f),  // 16 row 2 inner-right
        (0.974f, 0.273f),  // 18 row 2 right
        (0.026f, 0.500f),  // 19 mid-left
        (0.263f, 0.500f),  // 21 mid-inner-left
        (0.737f, 0.500f),  // 25 mid-inner-right
        (0.974f, 0.500f),  // 27 mid-right
        (0.026f, 0.727f),  // 28 row 4 left
        (0.263f, 0.727f),  // 30 row 4 inner-left
        (0.737f, 0.727f),  // 34 row 4 inner-right
        (0.974f, 0.727f),  // 36 row 4 right
        (0.026f, 0.954f),  // 37 bottom-left
        (0.263f, 0.954f),  // 39 bottom-inner-left
        (0.500f, 0.954f),  // 41 bottom-center
        (0.974f, 0.954f),  // 45 bottom-right
        (0.500f, 0.500f),  // 23 center (end)
    };

    private static readonly (float x, float y)[] CalibrationPoints_RightTilt =
    {
        (0.974f, 0.046f),  //  9
        (0.737f, 0.273f),  // 16
        (0.974f, 0.273f),  // 18
        (0.974f, 0.500f),  // 27
        (0.737f, 0.727f),  // 34
        (0.974f, 0.727f),  // 36
        (0.974f, 0.954f),  // 45
    };

    private static readonly (float x, float y)[] CalibrationPoints_LeftTilt =
    {
        (0.026f, 0.046f),  //  1
        (0.026f, 0.273f),  // 10
        (0.263f, 0.273f),  // 12
        (0.026f, 0.500f),  // 19
        (0.026f, 0.727f),  // 28
        (0.263f, 0.727f),  // 30
        (0.026f, 0.954f),  // 37
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
    // ConstantPixelSize: logical px == screen px, so this is exact.
    private static float NX(float n) => n * Screen.width;
    private static float NY(float n) => n * Screen.height;

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
                SetStatus($"✓ Calibration complete!\nAvg error: {err:F4}");
            else
                SetStatus("✓ Calibration complete!");

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

            StartCoroutine(FinishAndReturn(2f));
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

            // White index number centered inside
            var lbl = new Label((i + 1).ToString());
            lbl.style.position        = Position.Absolute;
            lbl.style.left            = 0; lbl.style.right = 0;
            lbl.style.top             = 0; lbl.style.bottom = 0;
            lbl.style.unityTextAlign  = TextAnchor.MiddleCenter;
            lbl.style.color           = Color.white;
            lbl.style.fontSize        = 13;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            d.Add(lbl);

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

