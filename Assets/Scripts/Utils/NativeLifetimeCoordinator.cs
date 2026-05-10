using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

public sealed class NativeLifetimeCoordinator : IDisposable
{
    private readonly ConcurrentDictionary<Guid, GCHandle> _handles = new();
    private readonly ConcurrentDictionary<Guid, IntPtr>   _comPtrs = new();

    private volatile bool _isShuttingDown = false;
    private int _activeCallbackCount = 0;
    private int _released = 0; // idempotency guard: 0=live, 1=released

    public Guid TrackManagedObject(object obj, GCHandleType handleType = GCHandleType.Normal)
    {
        if (_isShuttingDown)
            throw new InvalidOperationException("Cannot track resources during shutdown.");
        var id = Guid.NewGuid();
        _handles[id] = GCHandle.Alloc(obj, handleType);
        return id;
    }

    // NOTE: Not currently used by any component in this codebase.
    // Win32AudioInterop manages its own COM lifetime via _comLock and Shutdown().
    // If adding a new COM interop site, evaluate whether to use this or a dedicated
    // lock pattern — do NOT use both for the same object (double-release risk).
    public Guid TrackComObject(object comObj)
    {
        if (_isShuttingDown)
            throw new InvalidOperationException("Cannot track resources during shutdown.");
        var id = Guid.NewGuid();
        _comPtrs[id] = Marshal.GetIUnknownForObject(comObj); // AddRef
        return id;
    }

    public void Release(Guid id)
    {
        // Short-circuit: a Guid can only be in one dictionary
        if (_handles.TryRemove(id, out var handle))
        {
            if (handle.IsAllocated) handle.Free();
            return;
        }
        if (_comPtrs.TryRemove(id, out var punk) && punk != IntPtr.Zero)
            Marshal.Release(punk);
    }

    // Increment FIRST, then validate — closes the TOCTOU race where BeginShutdown
    // drains to zero between the check and the increment on another thread.
    public void EnterCallback()
    {
        Interlocked.Increment(ref _activeCallbackCount);
        if (_isShuttingDown)
        {
            Interlocked.Decrement(ref _activeCallbackCount);
            throw new InvalidOperationException("Cannot enter callback during shutdown.");
        }
    }

    public void ExitCallback() => Interlocked.Decrement(ref _activeCallbackCount);

    public void BeginShutdown()
    {
        _isShuttingDown = true;
        var sw = Stopwatch.StartNew();
        // Interlocked.CompareExchange forces a fresh volatile read each iteration
        while (Interlocked.CompareExchange(ref _activeCallbackCount, 0, 0) > 0
               && sw.ElapsedMilliseconds < 500)
            Thread.Sleep(10);
        ReleaseAll();
    }

    private void ReleaseAll()
    {
        // Interlocked.Exchange guarantees exactly one caller wins.
        // Double-free of a GCHandle is silent GC heap corruption — not an exception.
        if (Interlocked.Exchange(ref _released, 1) == 1) return;
        foreach (var id in _handles.Keys.ToArray())  Release(id);
        foreach (var id in _comPtrs.Keys.ToArray())  Release(id);
    }

    public void Dispose() => ReleaseAll();
}
