using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// Generic singleton base for all Python-bridge and external-process runner
/// MonoBehaviours in the Robit project.
///
/// Provides:
///   - SOLID singleton pattern (one static Instance per concrete type)
///   - DontDestroyOnLoad lifecycle
///   - Standardised process launch, stdout/stderr redirect, and teardown
///   - Virtual hooks so subclasses extend without modifying this class
///
/// UNITY NOTES
///   Generic MonoBehaviours are fully supported since Unity 2020.
///   Concrete subclasses are non-generic, so AddComponent{T}() and the
///   Inspector both work exactly as before.
///   [DefaultExecutionOrder] and [SerializeField] attributes belong on each
///   concrete class, not here.
/// </summary>
public abstract class BaseProcessRunner<T> : MonoBehaviour
    where T : BaseProcessRunner<T>
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static T Instance { get; private set; }

    // ── Abstract contract ─────────────────────────────────────────────────────

    /// <summary>
    /// Log prefix used by all base-class log calls, e.g. "[GazeFollowerRunner]".
    /// </summary>
    protected abstract string LogPrefix { get; }

    /// <summary>
    /// Called by OnApplicationQuit() and OnDestroy().
    /// Each concrete class implements the domain-specific teardown here,
    /// then the base handles Instance cleanup automatically.
    /// </summary>
    public abstract void StopRunner();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;

        // DontDestroyOnLoad ONLY works on root GameObjects.
        // If this component was added to a child object in the scene hierarchy,
        // we MUST detach it to the root first, otherwise it will be destroyed during scene loads!
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        
        OnAfterAwake();
    }

    /// <summary>
    /// Called at the end of Awake() after the singleton is established and
    /// DontDestroyOnLoad is set.  Override to perform subclass-specific init
    /// that must happen in Awake() (e.g. autostart logic).
    /// The default implementation is intentionally empty.
    /// </summary>
    protected virtual void OnAfterAwake() { }

    protected virtual void OnApplicationQuit()
    {
        StopRunner();
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopRunner();
    }

    // ── Process management helpers ────────────────────────────────────────────

    /// <summary>
    /// Builds a ProcessStartInfo with all the standard redirect flags set.
    /// WorkingDirectory is derived from the executable path.
    /// </summary>
    /// <param name="exePath">Absolute path to the executable.</param>
    /// <param name="arguments">Command-line arguments string.</param>
    /// <param name="redirectStdin">
    ///   Set true when the process needs graceful shutdown via stdin
    ///   (e.g. the camera-check process that reads "QUIT\n").
    /// </param>
    protected static ProcessStartInfo BuildProcessStartInfo(
        string exePath,
        string arguments,
        bool   redirectStdin = false)
    {
        return new ProcessStartInfo
        {
            FileName               = exePath,
            Arguments              = arguments,
            WorkingDirectory       = Path.GetDirectoryName(exePath),
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            RedirectStandardInput  = redirectStdin,
            CreateNoWindow         = true,
        };
    }

    /// <summary>
    /// Starts the given ProcessStartInfo, hooks stdout/stderr/exit to the
    /// virtual handler methods on this instance, and begins async reads.
    /// Returns the running Process.
    /// Throws on failure — callers should wrap in try/catch.
    /// </summary>
    protected Process StartManagedProcess(ProcessStartInfo psi)
    {
        var process = new Process
        {
            StartInfo          = psi,
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += OnOutputDataReceived;
        process.ErrorDataReceived  += OnErrorDataReceived;
        process.Exited             += OnProcessExited;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    /// <summary>
    /// Kills the process if still running, then unsubscribes all event handlers
    /// and disposes the process object.  Sets the ref to null.
    /// Use for processes that were started with <see cref="StartManagedProcess"/>.
    /// </summary>
    protected void TerminateProcess(ref Process process)
    {
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"{LogPrefix} Failed to stop process: {ex.Message}");
        }
        finally
        {
            UnhookAndDisposeProcess(ref process);
        }
    }

    /// <summary>
    /// Kills the process if still running and disposes it, WITHOUT unsubscribing
    /// event handlers.  Use for secondary processes (e.g. status probe, camera-check)
    /// that were NOT started via <see cref="StartManagedProcess"/> and therefore
    /// have no base-class handlers hooked.
    /// </summary>
    protected static void KillAndDispose(ref Process process)
    {
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch { /* Best-effort — caller may be in OnDestroy */ }
        finally
        {
            process.Dispose();
            process = null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void UnhookAndDisposeProcess(ref Process process)
    {
        if (process == null)
            return;

        process.OutputDataReceived -= OnOutputDataReceived;
        process.ErrorDataReceived  -= OnErrorDataReceived;
        process.Exited             -= OnProcessExited;
        process.Dispose();
        process = null;
    }

    // ── Virtual process event handlers ────────────────────────────────────────

    /// <summary>
    /// Called on a background thread when the managed process writes to stdout.
    /// The default implementation logs at Info level with the subclass LogPrefix.
    /// Override to add domain-specific stdout parsing (call base to keep the log).
    /// </summary>
    protected virtual void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.Log($"{LogPrefix}[PY] {e.Data.Trim()}");
    }

    /// <summary>
    /// Called on a background thread when the managed process writes to stderr.
    /// </summary>
    protected virtual void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"{LogPrefix}[PY-ERR] {e.Data.Trim()}");
    }

    /// <summary>
    /// Called on a background thread when the managed process exits.
    /// The default implementation logs the exit.
    /// Override to perform domain-specific cleanup (e.g. enqueue a reset value).
    /// </summary>
    protected virtual void OnProcessExited(object sender, EventArgs e)
    {
        RobitLogger.Log($"{LogPrefix} Process exited.");
    }
}
