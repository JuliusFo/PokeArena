# Flaky EventSub keepalive test — code review finding (2026-11-19, branch fix/flaky-keepalive-test)

- Built: `TimerCountingTimeProvider` (tests, a `FakeTimeProvider` that counts
  timers not yet disposed); `Missing_keepalive_ends_the_session_as_connection_lost`
  waits before each `Advance` until the session armed its next keepalive wait.
  Test code only — no CHANGELOG, no „Neuigkeiten“, no release needed.
- Cause: the session creates its keepalive timer only after the handler
  returned. The test saw the handler's text, advanced the clock before the new
  timer existed, the timer then started at the later time and never fired —
  `TimeoutException` after 5 s in roughly 2 of 35 Core runs (reviewer of
  fix/battle-log-hp-before). Reproduced every time with a 200 ms delay in the
  test handler; with the fix the class passes with that delay, the test 150
  times on its own and the whole Core suite 40 times in a row.
- User decisions: fixed in this session („Do this task here“).
- Pitfalls: the rule went to `src/PokeArena.Core/CLAUDE.md` (timers bullet).
