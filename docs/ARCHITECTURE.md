# LUCY AI — Architecture

## 1. Product boundary

LUCY AI is a software agent, not a fictional superintelligence. Its capabilities must be measurable and bounded by available models, data, hardware, permissions, and external services.

The system should remain useful when an optional model provider or network connection is unavailable. Core orchestration, policy enforcement, local records, and diagnostics should not depend on one model vendor.

## 2. High-level components

- **Application shell:** Windows desktop experience, settings, session view, approvals, diagnostics.
- **Agent orchestrator:** owns task state, step transitions, cancellation, retries, and budgets.
- **Reasoning/provider interface:** routes requests to configured local or remote model providers; providers are replaceable.
- **Planner:** decomposes goals into explicit, bounded steps with preconditions and expected outcomes.
- **Tool runtime:** typed tool contracts, capability registry, execution timeouts, cancellation, output limits, and isolation.
- **Policy engine:** authorizes actions before execution; applies least privilege and approval requirements.
- **Memory service:** working context, episodic records, semantic knowledge, and procedural patterns.
- **Research subsystem:** source collection, provenance, evidence grading, contradiction tracking, and summaries.
- **Verification subsystem:** checks postconditions, runs tests, compares expected and actual results, and decides whether to retry or stop.
- **Audit/telemetry:** structured events for decisions, tool calls, approvals, failures, and performance.
- **Storage:** SQLite as the initial durable store; schema migrations and export/import are required.

## 3. Current implementation slice

The first executable slice includes immutable task snapshots, a bounded task runner, a planner interface, a tool registry, per-step authorization, and a SQLite-backed task repository. Tool authorization denies by default. Task persistence currently stores task lifecycle snapshots; it is not yet a general-purpose memory system. Timeouts and caller cancellation are represented as distinct terminal task states. The runner does not provide automatic retries, process isolation, or recovery of in-flight tool side effects yet.

## 4. Cognitive cycle

1. Receive a goal and normalize constraints.
2. Retrieve relevant memory and known constraints.
3. Build a plan with bounded steps, risk labels, and success criteria.
4. Ask the policy engine to authorize each action.
5. Execute approved actions through typed tools.
6. Verify outputs and postconditions independently where possible.
7. On failure, classify the cause; retry only within explicit budgets.
8. Record outcome, evidence, and reusable procedure.
9. Report completed, failed, and unverified work distinctly.

A model-generated statement is not proof that an action happened. Tool responses, file state, test output, and external confirmations are the evidence.

## 5. Memory model

- **Working:** short-lived context for the active task.
- **Episodic:** timestamped tasks, actions, observations, and outcomes.
- **Semantic:** facts with provenance, confidence, timestamps, and contradiction links.
- **Procedural:** reusable workflows and tool-use patterns with preconditions.
- **Self-model:** configuration, known capabilities, limitations, and measured reliability—not a claim of consciousness.

Memory writes should be versioned where practical. Users must be able to inspect, correct, export, and delete stored information. Sensitive data should not be retained by default.

## 6. Autonomy and safety

Default policy:

- Read-only research and analysis: may run within configured source and resource limits.
- File creation and code changes: confined to an explicitly designated workspace; changes are diffed and tested.
- Network access: allowlisted and logged.
- Shell/process execution: disabled by default; explicit tool policy, working-directory restrictions, timeouts, and output caps required.
- Destructive actions, credential handling, privilege changes, purchases, external messages, and publication: require explicit user approval.
- Core self-update: never overwrite the active executable in place; require signed/verified artifacts, backup, health checks, and rollback.

The model must not be the sole authority deciding whether its own high-impact action is safe.

## 7. Reliability and evaluation

Every capability needs:
- a contract and explicit failure modes;
- unit and integration tests;
- deterministic fixtures where possible;
- structured logs and correlation IDs;
- resource/time budgets;
- a measurable acceptance criterion.

Track task completion, factual/source accuracy, tool success, recovery rate, memory retrieval precision, latency, cost, and regression rate. Do not optimize a single score at the expense of safety or correctness.

## 8. Proposed technology direction

C# is the initial implementation language. SQLite is the initial durable store. The desktop framework and .NET target remain an explicit decision because the current owner workstation is Windows 10 Pro and the current Microsoft support matrix for modern .NET on Windows 10 excludes Pro editions.

Avoid hard-coding provider SDKs into domain logic. Use interfaces and adapters. Select embedding/vector retrieval only after representative tests show a measurable benefit over simpler SQLite/full-text retrieval.

## 9. Initial non-goals

- Training a foundation model from scratch.
- Claiming consciousness or human-like subjective experience.
- Unrestricted self-modification or unrestricted shell access.
- Autonomous actions with irreversible external effects.
- Assuming local GPU inference is practical on the currently reported GT 610.
