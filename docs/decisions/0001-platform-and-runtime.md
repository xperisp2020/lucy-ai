# ADR 0001: Windows support and .NET runtime selection

- **Status:** Open — implementation target not yet selected.
- **Date:** 2026-11-22

## Context

The intended first deployment environment is Windows. The owner's reported workstation is Windows 10 Pro, build 19045 (22H2), x64, with .NET SDK 8.0.428 and .NET 8/10 runtimes installed. A runtime installation does not imply the corresponding SDK is installed.

Microsoft's current Windows installation guidance states that modern .NET support on Windows 10 is limited to LTSC and Enterprise editions. Windows 10 Pro is therefore outside the listed supported editions. The same guidance distinguishes SDK installation/build tooling from runtime execution.

Sources:
- https://learn.microsoft.com/en-us/dotnet/core/install/windows
- https://learn.microsoft.com/en-us/dotnet/core/releases-and-support

## Consequences

- Do not silently claim that .NET 10 is a supported production target for Windows 10 Pro.
- Do not select .NET 8 merely because SDK 8.0.428 is installed; its support lifecycle ends in November 2026 and it is not a sound new long-lived baseline.
- Keep the application framework decision open until the supported OS policy is settled.
- Evaluate either moving the development/deployment target to a supported Windows edition/version, or choosing a deliberately legacy-compatible target with clearly documented trade-offs.
- CI must test the actual declared target OS/runtime combination.

## Decision required

Choose the supported Windows baseline first. Then select the current supported .NET SDK and desktop framework that match it. Revisit this ADR when the compatibility test matrix is available.
