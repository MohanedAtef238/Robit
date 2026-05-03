---
title: "ADR-0001: Choice of Game Engine (Unity)"
status: "Accepted"
date: "2025-10-15"
authors: "Lead Architect"
tags: ["architecture", "engine", "decision"]
supersedes: ""
superseded_by: ""
---

#### Status

Accepted

#### Context

The Robit project requires a high-fidelity "Mock OS" environment that can simultaneously handle 2D desktop elements and advanced 3D visual effects. Traditional Windows frameworks (WPF, WinUI) lack the robust 3D/Shader capabilities needed for a "Next-Gen" aesthetic.

#### Decision

We use **Unity 6 (6000.x)** as the core engine for the Robit Mock OS.

#### Consequences

##### Positive

- **POS-001**: **Visual Fidelity**: Enables advanced 3D transitions and hardware-accelerated background effects.
- **POS-002**: **Prototyping Speed**: Access to the Unity Asset Store significantly reduces development time.
- **POS-003**: **C# Synergy**: Shared codebase between OS logic and Windows interop layers.

##### Negative

- **NEG-001**: **Resource Footprint**: High baseline memory (1GB+) and GPU usage.
- **NEG-002**: **Input Complexity**: Requires custom Win32 wrappers for global system input.

#### Alternatives Considered

##### WinUI 3 / WPF

- **ALT-001**: **Description**: Standard Microsoft Windows UI frameworks.
- **ALT-001**: **Rejection Reason**: Lacks out-of-the-box 3D shader support and performant background blurring.

#### Implementation Notes

- **IMP-001**: **Application.runInBackground**: Must be set to true.

#### References

- **REF-001**: [ADR-0003: Transparency Strategy](./adr-0003-transparency-strategy.md)
