---
title: "ADR-0004: FileSystem Virtualization for DesktopParser"
status: "Accepted"
date: "2026-01-20"
authors: "Lead Architect"
tags: ["architecture", "filesystem", "testing"]
supersedes: ""
superseded_by: ""
---

#### Status

Accepted (Implemented)

#### Context

`DesktopParser.cs` scans the Windows Start Menu. Direct `System.IO` calls made testing inconsistent across machines.

#### Decision

We virtualized file system access using an `IFileSystem` interface.

#### Consequences

##### Positive

- **POS-004**: **Environment Agnostic**: Tests pass on any machine.
- **POS-005**: **Performance**: Mock scanning is ~100x faster.

##### Negative

- **NEG-004**: **API Maintenance**: Interface must track `System.IO` changes.

#### Implementation Notes

- **IMP-006**: **IFileSystem Injection**: Defaults to physical disk in Play Mode.
