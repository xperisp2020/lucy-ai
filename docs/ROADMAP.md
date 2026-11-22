# Roadmap

This roadmap is gated: a stage is not complete until its acceptance criteria are demonstrated.

## Stage 0 — Repository and platform foundation

- [x] Confirm repository access, owner, default branch, and initial contents.
- [x] Record the project vision, boundaries, architecture, and safety principles.
- [x] Record the Windows/.NET compatibility issue.
- [x] Select .NET 10 LTS for the platform-neutral core library.
- [x] Add solution, core project, test project, formatting/analyzer baseline, and Windows CI.
- [ ] Verify the first CI run and resolve any failures.

**Exit gate:** clean checkout builds reproducibly on the declared core target; CI runs tests.

## Stage 1 — Reliable core

- [x] Immutable task model and bounded lifecycle transitions.
- [x] Vendor-neutral reasoning-provider contract.
- [x] Typed tool request/result/context contracts.
- [x] Deny-by-default tool authorization boundary.
- [x] Deterministic tests for task transitions and policy denial.
- [x] Add bounded task orchestration, deadlines, cancellation, and step budgets.
- [x] Add structured audit event contracts and an append-only SQLite audit log.
- [x] Add SQLite task persistence, schema version tracking, lookup, and stale-write protection.
- [ ] Add recovery semantics and integration tests for process interruption.

**Exit gate:** integration tests prove task lifecycle, persistence, cancellation, policy denial, and provider failure handling.

## Stage 2 — Memory and research

- Working and episodic memory.
- Provenance-aware semantic records.
- Search and retrieval evaluation using a small benchmark.
- Research pipeline with source capture and contradiction handling.

**Exit gate:** memory survives restart, can be inspected/exported/deleted, and retrieval quality is measured.

## Stage 3 — Planning and controlled execution

- Explicit plans, preconditions, postconditions, and budgets.
- Tool registry, allowlists, timeouts, and isolated workspaces.
- Independent result verification and bounded recovery.

**Exit gate:** multi-step benchmark tasks complete with auditable traces and no policy bypass.

## Stage 4 — Desktop experience

- Conversation/task interface.
- Settings for providers, memory, policies, and resource limits.
- Approval queue, audit viewer, and recovery controls.

**Exit gate:** user can understand and control the agent's actions; accessibility and UI tests pass.

## Stage 5 — Development assistant

- Repository inspection and patch generation.
- Build/test execution in disposable workspaces.
- Diff review, regression checks, and rollback.
- No direct mutation of the running core.

**Exit gate:** benchmarked repository tasks pass tests and preserve a recoverable baseline.

## Stage 6 — Multimodal and advanced capabilities

Only add image/audio/video capabilities when the hardware/provider path, privacy model, and evaluation datasets are defined.

**Exit gate:** modality-specific accuracy, latency, privacy, and failure behavior are measured.

## Ongoing quality gates

- No secrets in source control or logs.
- No unbounded loops, retries, or resource consumption.
- No claim of completion without observable evidence.
- Every new capability has tests, documentation, and an explicit permission model.
