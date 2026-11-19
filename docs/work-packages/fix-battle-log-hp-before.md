# Fight log: HP before the hit — viewer feedback (2026-11-18, branch fix/battle-log-hp-before)

- Built: `BattleLog.ToText` tracks every fighter's HP while it renders and
  labels a hit's target „HP before → after“ („Digda (Ash, HP 10 → 0)“);
  the attacker keeps its one number. Events stay as stored (HP after the
  step), so no migration — older fights change on „Alles neu
  veröffentlichen“. Website and desktop fight list both read these lines;
  chat never shows them. Tests: a hand-built log with an overkill hit (HP
  before over several rounds), updated engine, crit, generator, bUnit
  expectations; sample fights in `beispieldaten/kampf` follow the format.
  Spec §5, CHANGELOG, „Neuigkeiten“.
- User decisions: the small fix first, the fight log redesign later (ideas in
  backlog.md, „Fight log redesign“).
- Addenda: the format „HP 10 → 0“ (both numbers rather than only the HP
  before, as the viewer suggested) and version 1.7.2 were proposals
  (roadmap.md). Decided: released as 1.8.0 (user, 2026-11-19 at `/release`); the
  format lives on in 1.8.1 inside the user-picked hit bar („80 → 22“).
- Pitfalls: the damage in the text stays the full hit (46 on 10 HP left);
  the „Schaden“ box counts only what arrived — the arrow makes the
  difference visible.
- Deliberately not done (→ backlog): HP bars per line, shorter lines, duel
  sections, effectiveness badges, boss log filter.
