# Shorter boss cooldown for a while — streamer request (2026-11-10, branch feat/boss-cooldown-reduced)

- Built: mod command `!bosscooldown <Minuten> [Cooldown]` / `!bosscooldown
  aus` (`BossCooldownCommand`, logic in `BossEncounter.ReduceCooldownAsync` /
  `EndReductionAsync`); new table `BossCooldownReductions` (one row: cooldown
  and end, migration `BossCooldownReduction`), so the reduction survives a
  restart and the dry-run sandbox keeps its own. A fight takes the reduced
  cooldown if it ends before the reduction's end. A running fight cooldown
  shrinks by the factor new / its own length (`Cooldown.Length`, null for the
  failed-lobby retry, which is never shortened) and its ready announcement
  moves along (only if it had one). Spec §4/§8,
  chat texts 40–43, README, „So geht's“, „Neuigkeiten“ 1.5.0.
- User decisions: the reduction is a temporary event, not a setting, so a chat
  command is fine despite spec §8; a running cooldown shrinks too („Ja“); it
  runs by the clock, „maximal ein 24h-Limit“; „Eine wählbare Dauer wäre cool“
  → optional second number for the cooldown; release 1.5.0, WP-09 → 1.6.0.
- Addenda: Proposal (not confirmed) — minutes only, chosen cooldown 1 to
  `BossCooldownMinutes` − 1, `aus` keeps a running cooldown, the natural end is
  silent (roadmap.md).
- Deliberately not done: announcing the natural end, durations like „2h“,
  showing the reduction in the desktop app.
