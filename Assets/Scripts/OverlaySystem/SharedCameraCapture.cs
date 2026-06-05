using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using UnityEngine;

/// <summary>
/// Shared Camera Capture — Singleton, DontDestroyOnLoad.
///
/// ARCHITECTURE (v3 — External Writer):
///   share_camera.exe owns the webcam exclusively via OpenCV/DirectShow.
///   This script launches it, reads frames from the MMF it writes, and
///   exposes them as a Texture2D for Unity's UI preview.
///
///   Neither Unity nor the gaze bridge touch the webcam hardware directly.
///   Both just read from shared memory — a trivially cheap memory copy.
///
///   This eliminates all WebCamTexture issues:
///     - No GPU→CPU readback starvation
///     - No rendering pipeline dependency (no Canvas, no RenderTexture)
///     - No webcam handle conflicts between Unity and Python
///     - Works at full 1280×720 with zero frame drops
///
/// MMF FORMAT (written by share_camera.exe):
///   Offset  Size  Field
///   0       4     MAGIC (0x524F4254 = "ROBT")
///   4       4     VERSION (1)
///   8       4     width
///   12      4     height
///   16      4     format (1 = RGBA32)
///   20      8     frameId (int64)
///   28      8     timestamp (Windows FILETIME ticks)
///   36      4     activeBuffer (0 or 1)
///   40+     ...   pixel data (double-buffered, MAX_WIDTH * MAX_HEIGHT * 4 each)
/// </summary>
[DefaultExecutionOrder(-1000)]
public class SharedCameraCapture : MonoBehaviour
{
    const int MAGIC       = 0x524F4254;
    const int HEADER_SIZE = 40;
    const int MAX_WIDTH   = 1920;
    const int MAX_HEIGHT  = 1080;

    [SerializeField] string memoryName    = "RobitCameraFrame";
    [SerializeField] string relativeExePath = @"Multimodal_UDP/share_camera.exe";
    [SerializeField] int    deviceIndex   = 0;
    [SerializeField] int    requestedWidth  = 1280;
    [SerializeField] int    requestedHeight = 720;

    public static SharedCameraCapture Instance { get; private set; }

    // ── MMF reader ────────────────────────────────────────────────────────────
    MemoryMappedFile         mmf;
    MemoryMappedViewAccessor accessor;

    // ── Preview texture (updated from MMF each frame) ─────────────────────────
    Texture2D previewTexture;
    Color32[] readBuffer;
    long      lastReadFrameId = -1;

    // ── External process ──────────────────────────────────────────────────────
    Process writerProcess;

    public int       CurrentWidth   { get; private set; }
    public int       CurrentHeight  { get; private set; }
    public bool      IsReady        { get; private set; }
    public Texture2D PreviewTexture => previewTexture;

    // ── Public API ────────────────────────────────────────────────────────────

    public static void EnsureSpawned()
    {
        if (Instance != null) return;
        var go = new GameObject("[SharedCameraCapture]");
        go.AddComponent<SharedCameraCapture>();
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    System.Collections.IEnumerator Start()
    {
        yield return null;  // let first frame render

        // Launch the external camera writer first, then open the MMF it creates.
        LaunchWriter();

        // Give the writer a moment to create the MMF before we try to open it.
        yield return new UnityEngine.WaitForSecondsRealtime(1.5f);

        OpenOrCreateMMF();
    }

    void Update()
    {
        if (accessor == null) return;

        // ── Restart writer if it died ─────────────────────────────────────────
        if (writerProcess == null || writerProcess.HasExited)
        {
            RobitLogger.LogWarning("[SCC] Writer process exited — restarting.");
            LaunchWriter();
            return;
        }

        // ── Read header ───────────────────────────────────────────────────────
        int  magic     = accessor.ReadInt32( 0);
        int  width     = accessor.ReadInt32( 8);
        int  height    = accessor.ReadInt32(12);
        long frameId   = accessor.ReadInt64(20);
        int  activeBuf = accessor.ReadInt32(36);

        if (magic != MAGIC)           return;   // writer hasn't started yet
        if (width <= 0 || height <= 0) return;
        if (width > MAX_WIDTH || height > MAX_HEIGHT) return;
        if (frameId == lastReadFrameId) return;  // no new frame this Unity frame

        // ── Resize buffers if resolution changed ──────────────────────────────
        if (CurrentWidth != width || CurrentHeight != height)
        {
            CurrentWidth  = width;
            CurrentHeight = height;
            readBuffer    = new Color32[width * height];

            if (previewTexture != null) Destroy(previewTexture);
            previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            previewTexture.name = "SCC_Preview";
            RobitLogger.Log($"[SCC] Webcam feed locked to {width}×{height}.");
        }

        // ── Read pixel data from the active MMF buffer ────────────────────────
        long frameBytes = (long)width * height * 4;
        long offset     = HEADER_SIZE + (long)activeBuf * frameBytes;
        accessor.ReadArray(offset, readBuffer, 0, readBuffer.Length);

        // ── Push to Texture2D (CPU only, no GPU readback) ─────────────────────
        previewTexture.SetPixels32(readBuffer);
        previewTexture.Apply(false, false);

        lastReadFrameId = frameId;

        if (!IsReady)
        {
            IsReady = true;
            RobitLogger.Log($"[SCC] First frame read — READY (frameId={frameId}, {width}×{height}).");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        KillWriter();

        if (previewTexture != null) Destroy(previewTexture);
        accessor?.Dispose();
        mmf?.Dispose();
        RobitLogger.Log("[SCC] MMF released.");
    }

    void OnApplicationQuit()
    {
        KillWriter();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    void LaunchWriter()
    {
        string exePath = Path.Combine(Application.streamingAssetsPath, relativeExePath)
                             .Replace("/", "\\");

        if (!File.Exists(exePath))
        {
            RobitLogger.LogError($"[SCC] share_camera.exe not found at: {exePath}");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = exePath,
                Arguments              = $"--device {deviceIndex} --width {requestedWidth} --height {requestedHeight} --fps 30",
                WorkingDirectory       = Path.GetDirectoryName(exePath),
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            writerProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            writerProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    RobitLogger.Log($"[SCC-Writer] {e.Data}");
            };
            writerProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    RobitLogger.LogWarning($"[SCC-Writer] {e.Data}");
            };

            writerProcess.Start();
            writerProcess.BeginOutputReadLine();
            writerProcess.BeginErrorReadLine();
            RobitLogger.Log($"[SCC] share_camera.exe launched (PID {writerProcess.Id}).");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[SCC] Failed to launch share_camera.exe: {ex.Message}");
        }
    }

    void KillWriter()
    {
        if (writerProcess == null) return;
        try
        {
            if (!writerProcess.HasExited)
            {
                writerProcess.Kill();
                writerProcess.WaitForExit(2000);
            }
        }
        catch { /* process may have already exited */ }
        finally
        {
            writerProcess.Dispose();
            writerProcess = null;
        }
    }

    void OpenOrCreateMMF()
    {
        long maxFrame  = (long)MAX_WIDTH * MAX_HEIGHT * 4;
        long totalSize = HEADER_SIZE + maxFrame * 2;
        try
        {
            // Try to open the one the writer already created
            mmf = MemoryMappedFile.OpenExisting(memoryName);
            RobitLogger.Log($"[SCC] Opened existing MMF '{memoryName}'.");
        }
        catch
        {
            // Writer not ready yet — create our own so we don't block
            mmf = MemoryMappedFile.CreateOrOpen(memoryName, totalSize);
            RobitLogger.Log($"[SCC] Created MMF '{memoryName}' (writer not ready yet).");
        }
        accessor = mmf.CreateViewAccessor(0, 0);
    }
}
