# ADR 0001: Windows support and .NET runtime selection

- **Status:** Partially decided — core runtime selected; desktop target remains open.
- **Date:** 2026-11-22

## Context

The intended first deployment environment is Windows. The owner's reported workstation is Windows 10 Pro, build 19045 (22H2), x64, with .NET SDK 8.0.428 and .NET 8/10 runtimes installed. A runtime installation does not imply the corresponding SDK is installed.

Microsoft's current support policy lists .NET 10 LTS and .NET 11 STS as supported releases. .NET 10 is selected for the initial core because it is the current LTS line. The repository pins SDK 10.0.404 so CI and developer builds use a reproducible SDK feature band.

Microsoft's Windows installation guidance lists Windows 10 support for modern .NET only on specified LTSC/Enterprise editions; Windows 10 Pro is outside that support matrix.

Sources:
- https://dotnet.microsoft.com/en-us/platform/support/policy
- https://learn.microsoft.com/en-us/dotnet/core/install/windows
- https://learn.microsoft.com/en-us/dotnet/core/releases-and-support

## Decision

- Target the platform-neutral core library at `net10.0`.
- Use .NET 10 LTS rather than the now out-of-support .NET 8 line.
- Build and test the core on a current Windows GitHub-hosted runner.
- Keep the Windows desktop framework and supported deployment OS open until the Windows 10 Pro compatibility issue is resolved.
- Do not claim that GitHub-hosted CI proves compatibility with the owner's local workstation.

## Consequences

- The owner needs .NET SDK 10.0.404 or a compatible later feature-band SDK to build locally; the currently reported SDK 8.0.428 is insufficient.
- No WPF/WinUI desktop project is created until the deployment baseline is agreed.
- CI provides an early, repeatable quality gate independent of the owner's current local SDK.

## Revisit when

The supported Windows baseline is selected and a desktop UI compatibility test matrix can be executed.
