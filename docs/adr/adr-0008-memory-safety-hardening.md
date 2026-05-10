---
title: "ADR-0008: Deterministic Memory Safety & Native Interop Hardening"
status: "Accepted"
date: "2026-05-10"
authors: "Antigravity AI"
tags: ["architecture", "stability", "interop", "memory"]
supersedes: ""
superseded_by: ""
---

#### Status

Accepted

#### Context

Robit heavily relies on P/Invoke and COM interop for Windows desktop integration (e.g., volume control, window suppression, transparency). Previous implementations suffered from non-deterministic Garbage Collection (GC) behavior, leading to:

1. **Callbacks on Collected Delegates**: Native Windows threads calling into C# delegates that were already GC'd.
2. **COM Object Leaks**: MMDevice and Enumerator objects not being released, causing device invalidation errors.
3. **Shutdown Races**: Unity domain reloads or application quits triggering access violations when native callbacks executed against destroyed objects.

#### Decision

We implemented a comprehensive 7-component lifecycle hardening architecture centered around a centralized `NativeLifetimeCoordinator`.

Key architectural pillars:

1. **Centralized Coordination**: Use a `NativeLifetimeCoordinator` singleton to track all `GCHandle` and COM pointers.
2. **Thread-Safe Draining**: Implement a latch-based shutdown mechanism with a 500ms "drain" phase to quiesce active native callbacks.
3. **Pinned Delegate Promotion**: Replace anonymous lambdas in polling loops with pinned instance-field delegates.
4. **Idempotent UI Handling**: Use a `ScheduledItemHandle` wrapper for UI Toolkit tasks to prevent visual artifacts during rapid state changes.
5. **Stale Device Recovery**: Implement automatic re-acquisition of COM endpoints upon detecting `AUDCLNT_E_DEVICE_INVALIDATED`.

#### Consequences

##### Positive

- **POS-009**: **Crash Elimination**: Prevents "CallbackOnCollectedDelegate" and "AccessViolation" during shutdown/domain reload.
- **POS-010**: **Resource Integrity**: Ensures deterministic release of COM objects, preventing audio driver "stuck" states.
- **POS-011**: **Reduced GC Pressure**: Net performance gain through `StringBuilder` buffer reuse and COM object caching.

##### Negative

- **NEG-007**: **Shutdown Latency**: Adds up to 500ms delay during application quit to ensure native safety.
- **NEG-008**: **Interop Boilerplate**: Requires explicit `EnterCallback`/`ExitCallback` guards for all P/Invoke entry points.

#### Alternatives Considered

##### Pure Managed COM Wrappers (RCW)

- **ALT-007**: **Description**: Relying solely on the CLR to manage COM object lifetimes.
- **ALT-007**: **Rejection Reason**: Non-deterministic release caused "Device Busy" errors and prevented recovery from hardware changes.

##### Manual Unpinning in OnDestroy

- **ALT-008**: **Description**: Unpinning delegates directly in MonoBehaviour.OnDestroy.
- **ALT-008**: **Rejection Reason**: Does not handle native threads (like EnumWindows) that may still be in-flight during the frame the object is destroyed.

#### Implementation Notes

- **IMP-010**: **Execution Order**: `UnityNativeBridge` is assigned a Script Execution Order of `-100`.
- **IMP-011**: **Drain Logic**: Uses `Interlocked` primitives for the callback counter to ensure zero-lock overhead in high-frequency callbacks.
- **IMP-012**: **Mandatory `ExitCallback` in `finally`**: Every call to `EnterCallback()` must be paired with `ExitCallback()` inside a `finally` block. This is the mechanism that decrements `_activeCallbackCount` back to zero, allowing `BeginShutdown()`'s drain loop to unblock. If `ExitCallback()` were placed outside `finally`, an exception mid-callback would cause `_activeCallbackCount` to never reach zero, spinning the drain loop for the full 500ms timeout before releasing resources blindly — defeating the safety guarantee.
- **IMP-013**: **Volatile Read in Drain Loop**: The drain loop in `BeginShutdown()` uses `Interlocked.CompareExchange(ref _activeCallbackCount, 0, 0)` rather than reading `_activeCallbackCount` directly. This forces a fresh memory-barrier read on each iteration, preventing the JIT from register-caching the counter value and causing an infinite spin even after callbacks have exited.
- **IMP-014**: **Two-Level Drain**: Shutdown operates at two scopes. The global drain in `NativeLifetimeCoordinator.BeginShutdown()` guards application quit and domain reload. Individual components (e.g., `WindowsPopupSuppressor`) additionally maintain a local `_inCallback` flag that drives a component-level drain inside `StopSuppressing()`, allowing a component to be torn down safely mid-session without triggering a full application shutdown.

#### References

- **REF-007**: [GCHandle Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.gchandle)
- **REF-008**: [Unity Script Execution Order](https://docs.unity3d.com/Manual/class-MonoManager.html)
- **REF-009**: [AUDCLNT_E_DEVICE_INVALIDATED Handling](https://learn.microsoft.com/en-us/windows/win32/coreaudio/recovering-from-an-invalid-device-error)
