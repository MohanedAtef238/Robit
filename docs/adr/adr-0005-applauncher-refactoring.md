---
title: "ADR-0005: AppLauncher Decoupling for Testability"
status: "Accepted"
date: "2026-05-01"
authors: "Lead Architect"
tags: ["architecture", "testing", "refactor"]
supersedes: ""
superseded_by: ""
---

#### Status

Accepted (Implemented)

#### Context

`AppLauncher` was coupled to `System.Diagnostics.Process` and `SceneManager`, making unit testing impossible without side effects.

#### Decision

Decoupled execution logic using `IProcessRunner` and `ISceneLoader`.

#### Consequences

##### Positive

- **POS-001**: **Testability**: Achieved 100% coverage on coordination logic.
- **POS-002**: **Reliability**: Deterministic failure path testing.

##### Negative

- **NEG-001**: **Boilerplate**: Increased file count for abstractions.

#### Implementation Notes

- **IMP-004**: **Property Injection**: Uses auto-initialized defaults for runtime stability.
