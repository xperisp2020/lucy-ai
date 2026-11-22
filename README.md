# LUCY AI

LUCY AI is an experimental autonomous-assistant project inspired by the fictional system in *Lucy* (2014). The goal is to build the closest technically feasible software system—not to claim fictional or scientifically unsupported abilities.

## Project status

**Current stage:** Stage 1 — reliable core foundation (in progress).

The repository now contains an executable .NET core library, bounded task orchestration, per-tool authorization, a SQLite task repository, deterministic unit tests, and a Windows CI workflow. Model adapters, semantic/episodic memory, and the desktop interface are not implemented yet.

## Vision

Build a modular agent that can:

- maintain persistent, inspectable memory;
- research, reason, plan, and execute multi-step tasks through controlled tools;
- learn from recorded outcomes and improve plans;
- develop and test extensions in isolated workspaces;
- verify results, detect failures, and recover safely;
- support text and, later, additional modalities.

This project does **not** claim consciousness, human-equivalent understanding, telekinesis, time control, supernatural abilities, or a literal "100% brain usage" capability.

## Engineering principles

1. Evidence before assumptions; verify versions, APIs, and dependencies.
2. Separate planning from execution; validate tool inputs and outputs.
3. Least privilege, explicit policy boundaries, audit logs, and rollback.
4. Persistent memory must be inspectable, correctable, and deletable.
5. Self-improvement means controlled changes to plans, prompts, tools, and code—not self-modification of model weights.
6. Tests and measured results are required; never describe an unrun test as passing.

## Platform and runtime

The first implementation slice targets the cross-platform .NET 10 LTS core library. The Windows desktop framework and supported Windows baseline remain open decisions. The owner's reported Windows 10 Pro workstation is outside Microsoft's currently listed Windows 10 editions supported by modern .NET; therefore this repository does not yet claim that the application is supported on that workstation.

The CI workflow builds and tests on a current Windows GitHub-hosted runner. That verifies the core against the runner environment, not the owner's local Windows 10 Pro machine.

## Repository layout

- `src/Lucy.Core/` — domain, orchestration, policy, tool, and SQLite persistence components.
- `tests/Lucy.Core.Tests/` — deterministic unit tests.
- `docs/ARCHITECTURE.md` — system boundaries and proposed components.
- `docs/ROADMAP.md` — staged implementation and acceptance gates.
- `docs/decisions/` — documented engineering decisions.
- `.github/workflows/ci.yml` — Windows CI build and test.

## Local development

Install the .NET 10 SDK (the repository pins SDK 10.0.404 in `global.json`), then run:

```powershell
dotnet restore LUCY.slnx
dotnet build LUCY.slnx --configuration Release
dotnet test LUCY.slnx --configuration Release
```

## Status and verification

A successful local build/test has not yet been claimed. Check the latest GitHub Actions run for the authoritative CI result.

## License

No license has been selected yet. Until one is added, standard copyright applies; do not assume this repository grants permission to reuse its contents.
