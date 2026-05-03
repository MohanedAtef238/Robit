---
title: "ADR-0003: Transparency & Native Window Effects"
status: "Accepted"
date: "2025-12-12"
authors: "Lead Architect"
tags: ["architecture", "windows", "rendering"]
supersedes: ""
superseded_by: ""
---

#### Status

Accepted

#### Context

Robit must function as a high-fidelity overlay. Previous attempts using Unity URP shaders failed because Unity cannot natively "see" the desktop background.

#### Decision

We utilize **Windows 11 DWM (Desktop Window Manager)** attributes and **Layered Window Styles**.

#### Consequences

##### Positive

- **POS-007**: **Peak Performance**: OS handles the blur rendering.
- **POS-008**: **System Integration**: Perfect match for Windows 11 aesthetics.

##### Negative

- **NEG-006**: **OS Lock-in**: Requires Windows 11 for full effect.

#### Alternatives Considered

##### Unity URP Post-Processing

- **ALT-006**: **Description**: Blur shader on background camera.
- **ALT-006**: **Rejection Reason**: Cannot blur the actual desktop background.

#### Implementation Notes

- **IMP-008**: **DWM Extend Frame**: Margin must be set to `-1`.

#### References

- **REF-006**: [DwmSetWindowAttribute Documentation](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmsetwindowattribute)
