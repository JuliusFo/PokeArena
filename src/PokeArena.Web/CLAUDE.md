# PokeArena.Web — pitfalls

Public website, Blazor WebAssembly on GitHub Pages (spec §9). Visitors are casual viewers: simple German.

- References only `PokeArena.Contracts` (ADR-0009) — never Core: EF Core and the Twitch client must not end up in
  the browser download. Read the JSON with `SiteJson.Default.*` (source-generated, trim-safe), never reflection.
- Page components must not share a name with a Contracts record (`Leaderboard.razor` clashed with
  `Contracts.Leaderboard`; the leaderboard page is `Home.razor`).
- No official Pokémon assets, not even hot-linked (ADR-0008): species are text plus type chips (`Format.TypeClass`).
- Times: `Format.*` shows German time (Europe/Berlin), whatever the visitor's browser says.
- Polling pages inherit `PollingPage` (15 s, spec §13) and use the injected `TimeProvider`, so bUnit tests drive
  them with `FakeTimeProvider.Advance`. A battle page retries 404s: chat links can be faster than the upload.
- The data host comes from `wwwroot/appsettings.json` (`DataUrl`); the Pages workflow overwrites it from the
  repository variable `DATA_URL`. Locally (`--environment Development`, see `.claude/launch.json`) it reads the
  invented files in `wwwroot/beispieldaten/` — never put real player data there (ADR-0003).
- GitHub Pages has no SPA routing: the workflow copies `index.html` to `404.html` and adds `.nojekyll`
  (`_framework`). Links in markup are relative (`kampf/12`, not `/kampf/12`) so they follow `<base href>`.
- `index.html` uses the .NET 10 asset placeholders (`#[.{fingerprint}]`, `OverrideHtmlAssetPlaceholders`).
- `Pages/News.razor` („Neuigkeiten“) is hand-written for players, not the CHANGELOG: a new section goes on top as
  `<h2>Version X.Y.Z…</h2>` inside `.news` — the bUnit test parses that heading. At 375 px the menu already wraps to
  a second line with four items; links never break inside (`nowrap`, WP-10).
- Contrast (WCAG AA 4.5 : 1): never fade text that carries information with `opacity`, and `--accent` on white is
  only ≈ 4.4 : 1 — fine for links, not for small status text. Measure light and dark in the preview
  (feat/battle-end-state).
- Colours nobody chose for readability (chat colours, type colours as text) go through `Tint.Style` + class `tinted`,
  never straight into `color`. `Tint.TypeColors` and the surfaces mirror site.css; `TintTests` fails if they drift
  (feat/fight-log-colours). Yellow/gold as text turns brown on white at 4.5 : 1 — use it as a background with dark
  text (`.log .critical`, review feat/fight-log-boss-name-crit).
- A fighter row is `.who` (species + owner, wraps, `flex: 1 1 0`, never `min-width: 0` — the name would cover the
  HP text) next to `.end-state` (never shrinks): never put a new element straight into `.fighters li`, or long
  names push the HP bar onto its own line again (a bUnit test checks the children). Only `.sides` is wider
  than the page's 868 px content (1008 px from 1100 px viewport; `box-sizing: border-box`), so the header doesn't jump (fix/fighter-row-wrap).
- Never tell a boss or NPC by `TrainerId == null`: a deleted trainer's fighters have no id either („[gelöscht]“). The
  boss is the fighter on side 1 of a boss fight (review feat/fight-log-colours).
