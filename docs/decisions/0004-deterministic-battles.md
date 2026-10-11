# ADR-0004: Deterministic battles, logs as data

Status: accepted · Date: 2026-09-03

## Context

Battles must be unit-testable and explainable after the fact, while balancing
values change at runtime.

## Decision

- Randomness and time are injected (RNG and clock interfaces); Core never
  calls `Random`, `DateTime.Now` or `DateTimeOffset.Now` directly.
- Every battle stores its seed **and** its full log verbatim. The log is data,
  not derived: replaying a seed after a balancing change would give a
  different outcome.
- `MaxRunden` is only a safety net; reaching it is logged as a warning (bug).

Deliberately **not**: reconstructing battle logs from seeds.

## Consequences

- Golden tests per seed are possible but pin a fixed config; they break by
  design when formulas change.
- Battle logs take DB space; a retention policy is deferred (spec §14).
