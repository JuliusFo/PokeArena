# Fight log: boss name only, critical hits — user request (2026-11-10, branch feat/fight-log-boss-name-crit)

- Built: `BattleLog.ToText` splits a label into species (`IsSpecies`), „ (“,
  owner, rest, and „ Volltreffer!“ into a part of its own (`IsCritical`);
  `LogPart` carries both flags (left out of the JSON when false). Web:
  `Tint.Of` colours only the boss's species part; „Volltreffer!“ renders as
  `<strong class="critical">`: bold, dark text on gold (`#f2b500`), the same in
  both themes; the leading space stays outside the highlight. Spec §9 ³,
  CHANGELOG, „Neuigkeiten“, sample fight 1. Tests: log parts with and without
  effectiveness, generator parts and JSON, page colours (boss name only, crit as
  `<strong>`), crit text contrast on its background in site.css.
- User decisions: „Beim Boss … nur den Namen farbig machen, aber nicht alles
  was dann noch kommt in den Klammern“; „Einen Volltreffer Fett/Gold markieren“.
  After the code review: gold as a highlight, not as text colour („Goldene
  Markierung klingt gut“) — gold text turns brown on white at 4.5 : 1 and looks
  like an Elektro boss; the uncoloured boss in older files and version 1.4.1
  stay („Der Rest passt so“). Second review pass: a crit part without text
  crashed the page — now plain text („Yes, fix it“).
- Pitfalls: files written before have no `isSpecies` — their boss stays
  uncoloured (not the whole label as before) until „Alles neu veröffentlichen“.
  Plain text (`lines`, desktop, chat) is unchanged.
