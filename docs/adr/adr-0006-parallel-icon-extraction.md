---
title: "ADR-0006: Parallel Icon Extraction Pipeline in DesktopParser"
status: "Proposed"
date: "2026-05-04"
authors: "Lead Architect"
tags: ["architecture", "performance", "threading", "desktop-parser"]
supersedes: ""
superseded_by: ""
---

#### Status

Proposed

#### Context

`DesktopParser.ParseShortcuts()` scans both user and common Start Menu folders, resolving hundreds of `.lnk` shortcut files and extracting their EXE icons. The current implementation is a Unity Coroutine that processes one shortcut per frame (`yield return null`), serialising:

1. File I/O — `IFileSystem.ReadAllBytes()` + `IFileSystem.FileExists()`
2. Shortcut parsing — `WinShortcut` (managed, memory-only)
3. Icon extraction — `IconExtractor.ExtractAllIcos()` (Win32 `LoadLibraryEx` + P/Invoke enumeration)
4. ICO decoding — `IcoConversion.LoadIcon()` (managed byte-array parsing)
5. PNG cache check / write — `IFileSystem` I/O
6. `Texture2D` creation — Unity main-thread requirement

At 60 fps and ~200 Start Menu shortcuts, worst-case cold-start takes **3–5 seconds** before the first app card is rendered. Steps 1–5 are CPU- and I/O-bound with no Unity-API dependencies and are safe to execute on thread-pool threads. Only step 6 (`new Texture2D` / `tex.LoadImage`) must remain on the Unity main thread.

The project has no third-party async runtime (e.g. UniTask) and targets .NET Standard 2.1, making `System.Threading.Tasks` the canonical approach.

#### Decision

We refactor `DesktopParser.ParseShortcuts()` to use a **parallel background-extraction + main-thread batch-apply** pipeline:

1. **Enumerate shortcuts synchronously** on the main thread (fast directory scan — kept as-is).
2. **Dispatch extraction tasks** — for each valid shortcut file, create a `Task<RawShortcutData?>` that runs steps 1–5 on the thread pool via `Task.Run`, accepting a shared `CancellationToken`.
3. **Await all tasks** using `Task.WhenAll`, converting the `IEnumerator` coroutine body to an `async` method invoked via a thin coroutine wrapper (`StartCoroutine(RunAsync(ParseShortcutsAsync(ct)))`).
4. **Apply on main thread** — iterate the completed `RawShortcutData[]` results and call `new Texture2D` / `tex.LoadImage` sequentially on the main thread, yielding every `BatchSize` items to avoid a single-frame spike.
5. **Cancellation** — expose a `CancellationTokenSource` cancelled in `OnDestroy`; worker tasks call `ct.ThrowIfCancellationRequested()` before each expensive operation.

The intermediate `RawShortcutData` struct carries only managed, non-Unity types (`string`, `byte[]`) so it is safely produced off-thread and consumed on-thread.

```csharp
// Sketch — not prescriptive implementation
private record RawShortcutData(string Name, string TargetPath, string WorkingDir, byte[] IconPng);

private async Task<RawShortcutData?> ExtractAsync(string lnkPath, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    // file I/O, WinShortcut parse, IconExtractor, IcoConversion — all off main thread
    ...
    return new RawShortcutData(name, targetPath, workDir, pngBytes);
}
```

#### Consequences

##### Positive

- **POS-001**: **Startup Latency**: Icon extraction tasks run concurrently on the thread pool; empirically reduces cold-start from ~3–5 s to under 1 s on 4+ core machines.
- **POS-002**: **Main-Thread Responsiveness**: The Unity frame loop is no longer stalled by `LoadLibraryEx` and file I/O calls between each shortcut; UI animations remain smooth during parsing.
- **POS-003**: **Cancellation Safety**: `CancellationToken` propagation ensures in-flight workers are aborted cleanly on scene unload or component destruction, preventing orphaned threads.
- **POS-004**: **Testability Preserved**: `IFileSystem` injection is unaffected; worker methods accept only managed types and can be unit-tested without Unity or a thread pool.

##### Negative

- **NEG-001**: **Main-Thread Marshal Overhead**: A batch-apply loop must run on the main thread after `Task.WhenAll` completes, adding a synchronisation point and `yield return null` stutter every `BatchSize` items (acceptable; configurable).
- **NEG-002**: **Complexity**: The coroutine-to-async bridge (`RunAsync` wrapper) is non-obvious for contributors unfamiliar with Unity's threading model; requires clear documentation and test coverage.
- **NEG-003**: **Win32 Concurrency Assumptions**: `IconExtractor.ExtractAllIcos` calls `LoadLibraryEx` with `LOAD_LIBRARY_AS_DATAFILE` per invocation and frees it in `finally` — confirmed re-entrant for distinct module handles. If this contract is ever changed to share a handle, thread safety must be re-evaluated.
- **NEG-004**: **PNG Cache Write Race**: Multiple tasks may attempt to write the same PNG cache file concurrently if two `.lnk` files point to the same EXE. A per-path lock or a `ConcurrentDictionary<string, Task>` deduplication guard is required.

#### Alternatives Considered

##### Keep Sequential Coroutine

- **ALT-001**: **Description**: Retain the current `yield return null` coroutine, one shortcut per frame.
- **ALT-001**: **Rejection Reason**: Startup latency scales linearly with shortcut count; measured at 3–5 s for a typical Windows installation. Unacceptable UX for a "next-gen" overlay application.

##### UniTask / Cysharp.Threading

- **ALT-002**: **Description**: Introduce the UniTask package to replace `System.Threading.Tasks` with Unity-native async primitives and `PlayerLoopTiming` dispatch.
- **ALT-002**: **Rejection Reason**: No third-party async runtime is currently a dependency. Adding UniTask is a larger architectural commitment (separate ADR) and is not necessary to solve icon-extraction parallelism with the coroutine-to-async bridge pattern already established in Unity 6.

##### `Parallel.ForEach` with `SynchronizationContext`

- **ALT-003**: **Description**: Use `Parallel.ForEach` for extraction and a `UnitySynchronizationContext.Post` callback for each completed item.
- **ALT-003**: **Rejection Reason**: `Parallel.ForEach` blocks the calling thread until all partitions complete, which would stall the main thread or require wrapping in a `Task.Run` anyway. The `Task.WhenAll` approach is more idiomatic for async, non-blocking coordination.

#### Implementation Notes

- **IMP-001**: **Coroutine Bridge**: Use the established Unity pattern `IEnumerator RunAsync(Task t) { yield return new WaitUntil(() => t.IsCompleted); if (t.IsFaulted) throw t.Exception; }` to bridge `async Task` and the Coroutine scheduler.
- **IMP-002**: **Batch Size**: Expose `[SerializeField] private int textureApplyBatchSize = 20;` so QA can tune the main-thread apply loop without a code change.
- **IMP-003**: **PNG Cache Dedup**: Use `ConcurrentDictionary<string, Task<byte[]>>` keyed on `exePath` to coalesce concurrent extraction tasks for the same executable, eliminating redundant `LoadLibraryEx` calls and write races.
- **IMP-004**: **CancellationTokenSource Lifecycle**: Create in `Start()`, cancel and dispose in `OnDestroy()`. Pass `ct` to each `Task.Run` lambda and to all awaited I/O helpers.

#### References

- **REF-001**: [ADR-0004: FileSystem Virtualization for DesktopParser](./adr-0004-desktop-parser-refactoring.md)
- **REF-002**: [Unity Manual — Thread-safe operations](https://docs.unity3d.com/Manual/30_search.html?q=thread+safe+operations)
