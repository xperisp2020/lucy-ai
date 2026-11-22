# LUCY AI

LUCY AI is an experimental, Windows-oriented autonomous assistant project inspired by the fictional system in *Lucy* (2014). The goal is to build the closest technically feasible software system—not to claim fictional or scientifically unsupported abilities.

## Project status

**Stage:** architecture and engineering foundation.

The repository currently contains the initial architecture, constraints, roadmap, and contribution/security guidance. Runtime capabilities are not implemented yet.

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

## Platform decision

The initial target environment is Windows. The owner's current workstation is Windows 10 Pro (22H2), with .NET SDK 8.0.428 and .NET 8/10 runtimes. Microsoft currently lists modern .NET support on Windows 10 as limited to LTSC/Enterprise editions; Windows 10 Pro is outside that support matrix. The project therefore records the platform/runtime choice as an open decision rather than silently selecting an unsupported target.

See [Architecture](docs/ARCHITECTURE.md), [Roadmap](docs/ROADMAP.md), and [Platform decision](docs/decisions/0001-platform-and-runtime.md).

## Repository layout

- `docs/ARCHITECTURE.md` — system boundaries and proposed components.
- `docs/ROADMAP.md` — staged implementation and acceptance gates.
- `docs/decisions/` — documented engineering decisions.
- `SECURITY.md` — security reporting and safety boundaries.
- `CONTRIBUTING.md` — development and verification rules.

## Status and verification

No executable application or automated test suite has been added yet. The next implementation gate is to resolve the supported Windows/runtime target, then establish the solution structure and CI before adding agent capabilities.

## License

No license has been selected yet. Until one is added, standard copyright applies; do not assume this repository grants permission to reuse its contents.
