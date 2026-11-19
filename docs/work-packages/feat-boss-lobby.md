# Boss lobby — streamer requests (2026-11-08, branch feat/boss-lobby)

- Built: `BossCountdownSeconds` 60 → 120, announced as „noch 2 Minuten“
  (seconds if not whole minutes); a first join with ≤ 10 s left moves the
  lobby's end back by `BossLateJoinExtensionSeconds` (10) — the countdown work on
  the queue re-schedules itself for the rest; an escaped boss reports „hatte
  noch X von Y HP“ (`BossBattleResult.BossHp`/`BossMaxHp`). Spec §4/§13,
  chat-texts rows 9, 13, 14, „So geht's“ page updated; tests.
- User decisions (proposed defaults, accepted with the go-ahead): the
  extension is +10 s on top of the end (not „reset to 10 s“), silent in chat,
  no cap; HP text with the start value because boss HP grows with the
  participant count.
- Deliberately not done (→ backlog): setting the countdown length without a
  release — not as a chat command (spec §8), at most as a desktop setting.
