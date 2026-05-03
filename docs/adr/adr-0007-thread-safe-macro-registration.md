---
title: "ADR-0007: Thread-Safe MacroAction Self-Registration"
status: "Proposed"
date: "2026-05-04"
authors: "Lead Architect"
tags: ["architecture", "threading", "macro-system", "startup"]
supersedes: ""
superseded_by: ""
---

#### Status

Proposed

#### Context

`MacroActionFactory` uses a self-registration pattern where each action class writes its factory delegate into a shared registry via `MacroActionFactory.Register()` during its static constructor. The `Bootstrap()` method — annotated `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` — forces all 25+ static constructors to execute by calling `RuntimeHelpers.RunClassConstructor` sequentially.

Two problems arise as the action count grows:

1. **Sequential execution**: Each `RunClassConstructor` call is a synchronous blocking operation. With 25 calls today, and more expected as the macro library expands, the aggregate cost (JIT compilation + static initialisation) grows linearly and occurs on the main thread before any scene is loaded.

2. **Non-thread-safe registry**: The backing store is `Dictionary<MacroActionType, Func<IMacroAction>>`. If `Bootstrap()` is ever parallelised — or if future code accidentally triggers a static constructor off the main thread — concurrent writes will cause silent data corruption or `ArgumentException` from duplicate-key insertion.

The `MacroActionFactory` is the sole consumer of this registry; it is write-once (all writes happen during bootstrap) and read-many thereafter. This profile is ideal for a `ConcurrentDictionary` with `TryAdd` semantics, and the sequential bootstrap loop is a candidate for `Parallel.ForEach`.

#### Decision

We make two coordinated changes:

1. **Replace `Dictionary` with `ConcurrentDictionary`** for the action registry, changing `_registry[type] = factory` to `_registry.TryAdd(type, factory)` in `Register()`. This eliminates the thread-safety defect unconditionally, regardless of whether parallelism is introduced.

2. **Parallelise `Bootstrap()` using `Parallel.ForEach`** over the set of `RuntimeTypeHandle` values, invoking `RuntimeHelpers.RunClassConstructor` for each. The JIT and static-initialisation work is distributed across thread-pool threads, reducing the total blocking time on the main thread before the first scene loads.

```csharp
// Before
private static void Bootstrap()
{
    RuntimeHelpers.RunClassConstructor(typeof(AppCyclerAction).TypeHandle);
    RuntimeHelpers.RunClassConstructor(typeof(BackAction).TypeHandle);
    // ... 23 more sequential calls
}

// After
private static readonly RuntimeTypeHandle[] _actionTypes =
{
    typeof(AppCyclerAction).TypeHandle,
    typeof(BackAction).TypeHandle,
    // ... all action types
};

private static void Bootstrap()
{
    Parallel.ForEach(_actionTypes, handle =>
        RuntimeHelpers.RunClassConstructor(handle));
}
```

The `Register()` method changes from:
```csharp
// Before — not thread-safe
_registry[type] = factory;

// After — thread-safe, idempotent
_registry.TryAdd(type, factory);
```

`Create()` is read-only and already safe; no changes required there.

#### Consequences

##### Positive

- **POS-001**: **Thread Safety**: `ConcurrentDictionary` eliminates the latent data-race defect on the registry, making the factory safe to consume from any thread in future call sites.
- **POS-002**: **Startup Performance**: Parallel static constructor invocation distributes JIT and initialisation cost across available cores. On a 4-core machine with 25 actions, theoretical speedup is ~4x for the bootstrap phase (actual gain depends on JIT hot-path sharing, typically 2–3x measured).
- **POS-003**: **Scalability**: Adding new `MacroActionType` entries and their action classes requires no change to `Bootstrap()` structure — only appending to `_actionTypes`, which is consistent with the existing open/closed pattern.

##### Negative

- **NEG-001**: **`TryAdd` Silences Duplicates**: Changing from `_registry[type] = factory` to `TryAdd` means a duplicate registration (e.g. a copy-paste error creating two classes for the same `MacroActionType`) will silently use the first registration instead of throwing. A `Debug.Assert` or a `Create()` audit test should compensate.
- **NEG-002**: **Diagnostic Complexity**: If a static constructor throws during parallel bootstrap, `Parallel.ForEach` wraps the exception in an `AggregateException`, which is less ergonomic than a direct `TypeInitializationException`. The `Bootstrap()` method must catch and unwrap `AggregateException` for logging before rethrowing.
- **NEG-003**: **Marginal Gain at Current Scale**: With 25 action types and sub-millisecond static constructors, the observable startup improvement is small (~2–5 ms). The primary motivation is correctness (`ConcurrentDictionary`) and future-proofing as the macro library grows.

#### Alternatives Considered

##### Keep Sequential Bootstrap, Add Only `ConcurrentDictionary`

- **ALT-001**: **Description**: Apply only the `ConcurrentDictionary` fix and leave `Bootstrap()` sequential.
- **ALT-001**: **Rejection Reason**: Acceptable as a minimal fix. However, it foregoes the scalability benefit and leaves a code pattern that will need revisiting as action count grows. Adopting parallelism now, while the change is trivial, avoids a future refactor under time pressure.

##### Source Generator / Reflection-Free Registration

- **ALT-002**: **Description**: Use a Roslyn source generator to emit the `Bootstrap()` body at compile time, eliminating `RuntimeHelpers.RunClassConstructor` entirely.
- **ALT-002**: **Rejection Reason**: Source generators add toolchain complexity and a non-trivial maintenance surface. The self-registration pattern with `[Preserve]` attributes already satisfies Unity IL2CPP requirements. A source generator is over-engineering for the current scale.

##### `[RuntimeInitializeOnLoadMethod]` per Action Class

- **ALT-003**: **Description**: Annotate each action class with its own `[RuntimeInitializeOnLoadMethod]` attribute, letting Unity drive registration independently without a central `Bootstrap()`.
- **ALT-003**: **Rejection Reason**: Unity does not guarantee the execution order of multiple `[RuntimeInitializeOnLoadMethod]` callbacks at the same `LoadType`. If `Create()` is called before all callbacks have fired, it will silently return `null` for unregistered types. The central `Bootstrap()` provides a clear, auditable registration fence.

#### Implementation Notes

- **IMP-001**: **`_actionTypes` Array**: Declare as `private static readonly RuntimeTypeHandle[]` populated with `typeof(X).TypeHandle` for each action. Maintaining this list is equivalent to the existing `Bootstrap()` body — no additional discipline required.
- **IMP-002**: **`AggregateException` Handling**: Wrap `Parallel.ForEach` in `try/catch (AggregateException ae)`, log each inner exception via `RobitLogger.LogError`, then rethrow the first inner exception so Unity's error reporting surfaces a clear `TypeInitializationException`.
- **IMP-003**: **Duplicate Registration Test**: Add a test in `Robit.Tests` that calls `MacroActionFactory.Bootstrap()` twice and asserts the registry count equals `MacroActionType` enum values minus `None`, confirming idempotence of `TryAdd`.
- **IMP-004**: **`ConcurrentDictionary` Read Path**: `_registry.TryGetValue` in `Create()` is already the correct concurrent-read API; no change needed.

#### References

- **REF-001**: [ADR-0005: AppLauncher Decoupling for Testability](./adr-0005-applauncher-refactoring.md)
- **REF-002**: [Microsoft Docs — ConcurrentDictionary\<TKey,TValue>](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2)
