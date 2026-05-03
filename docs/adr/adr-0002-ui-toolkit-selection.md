---
title: "ADR-0002: UI Framework Selection (UI Toolkit)"
status: "Accepted"
date: "2025-10-28"
authors: "Lead Architect"
tags: ["architecture", "ui", "framework"]
supersedes: ""
superseded_by: ""
---

#### Status

Accepted

#### Context

The Mock OS requires a highly flexible, style-driven UI system. Traditional Unity UI (UGUI) lacks a centralized styling system.

#### Decision

We use **Unity UI Toolkit** (UXML/USS) as the primary UI framework.

#### Consequences

##### Positive

- **POS-010**: **Maintainability**: Centralized CSS-like styling.
- **POS-011**: **Scene Hygiene**: Minimizes GameObjects in the scene hierarchy.

##### Negative

- **NEG-009**: **Learning Curve**: Requires knowledge of Flexbox and CSS.

#### Alternatives Considered

##### Classic UGUI (Canvas)

- **ALT-008**: **Description**: The legacy GameObject-based UI system.
- **ALT-008**: **Rejection Reason**: Lacks a real styling system.

#### Implementation Notes

- **IMP-011**: **Styling**: All colors must be defined in central `.uss` files.

#### References

- **REF-008**: [Unity UI Toolkit Documentation](https://docs.unity.com/manual/UIElements.html)
