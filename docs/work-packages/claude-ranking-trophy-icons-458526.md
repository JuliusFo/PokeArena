# Leaderboard trophies — user request (2026-11-19, branch claude/ranking-trophy-icons-458526)

- Built: `Shared/Trophy.razor`, an 18 px inline SVG (cup, handles, stem,
  base) before the rank number of ranks 1–3 on the leaderboard, gold, silver
  and bronze through CSS custom properties (`.trophy.rank-N`), each with a
  darker edge so silver stays visible on white; same colours in both themes,
  except the stroke-only handles, which take the metal on the dark page.
  `aria-hidden` (the number carries the rank). Leaderboard ranks are unique
  (`SiteGenerator` breaks every tie), so there is no shared trophy.
  bUnit test (ranks 1–5), spec §9 ⁸, CHANGELOG, „Neuigkeiten“.
- User decisions: —
- Addenda: —
- Pitfalls: no emoji — 🏆/🥇 render differently per system and only the gold
  cup exists as a trophy emoji (now in the Web pitfalls).
- Deliberately not done (→ backlog): —
- Code review (2026-11-19): user decision „Alle beheben“.
  - L1 docs and test assumed shared ranks, the leaderboard has none: fixed
    (wording dropped, test with ranks 1–5).
  - L2 comment named a non-existent branch: fixed.
  - L3 emoji pitfall missing in the Web CLAUDE.md, trophies missing in spec
    §9: fixed (pitfall line, footnote ⁸).
  - L4 bronze handles faint on the dark page (edge ≈ 2 : 1): fixed, the
    handles take the metal colour in the dark theme.
  - Second pass: the four fixes hold; one new point. L5 spec footnote ⁸
    said „exactly three“ and that the documented sort breaks every tie (only
    the unstated Twitch id tiebreak does): fixed (id named in the sort,
    „at most three“).
