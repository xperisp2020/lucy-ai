# Contributing

## Engineering workflow

1. Inspect the current source, documentation, dependency versions, and CI before changing behavior.
2. State the intended behavior and failure cases.
3. Prefer small, reviewable changes with clear ownership boundaries.
4. Add or update tests for behavior changes and regressions.
5. Run formatting, analyzers, build, and tests; report exact commands and outcomes.
6. Update architecture/decision documents when changing boundaries or dependencies.

## Quality rules

- Enable nullable reference types when the .NET project is established.
- Prefer explicit contracts, dependency injection at composition boundaries, and deterministic tests.
- Avoid hidden global state and unbounded retries.
- Keep I/O at infrastructure boundaries; keep domain logic testable.
- Do not add a dependency without documenting why it is needed.
- Never claim a test passed unless it was actually run.

## Commits

Use concise imperative commit messages. Attribute authorship accurately; do not impersonate another account or falsify commit metadata.
