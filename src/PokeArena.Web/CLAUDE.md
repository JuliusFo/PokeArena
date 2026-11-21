# PokeArena.Web — pitfalls

Public website, Blazor WebAssembly on GitHub Pages (spec §9). Visitors are casual viewers: simple German.

- References only `PokeArena.Contracts` (ADR-0009) — never Core: EF Core and the Twitch client must not end up in
  the browser download. Read the JSON with `SiteJson.Default.*` (source-generated, trim-safe), never reflection.
- Page components must not share a name with a Contracts record (`Leaderboard.razor` clashed with
  `Contracts.Leaderboard`; the leaderboard page is `Home.razor`).
- No official Pokémon assets, not even hot-linked (ADR-0008): species are text plus type chips (`Format.TypeClass`).
  The viewers' Twitch profile pictures are viewer content outside that ban (ADR-0008 addendum, 1.7.0).
- Times: `Format.*` shows German time (Europe/Berlin), whatever the visitor's browser says.
- Polling pages inherit `PollingPage` (15 s, spec §13) and use the injected `TimeProvider`, so bUnit tests drive
  them with `FakeTimeProvider.Advance`. A battle page retries 404s: chat links can be faster than the upload.
- The data host comes from `wwwroot/appsettings.json` (`DataUrl`); the Pages workflow overwrites it from the
  repository variable `DATA_URL`. Locally (`--environment Development`, see `.claude/launch.json`) it reads the
  invented files in `wwwroot/beispieldaten/` — never put real player data there (ADR-0003).
- GitHub Pages has no SPA routing: the workflow copies `index.html` to `404.html` and adds `.nojekyll`
  (`_framework`). Links in markup are relative (`kampf/12`, not `/kampf/12`) so they follow `<base href>`.
- `index.html` uses the .NET 10 asset placeholders (`#[.{fingerprint}]`, `OverrideHtmlAssetPlaceholders`) — they
  resolve only for JS. `css/site.css` keeps its name; `pages.yml` appends its content hash as `?v=`, or a browser
  pairs a cached old stylesheet with new markup (HP numbers twice in Firefox after 1.6.0, fix/css-cache-busting).
  Keep the link exactly `href="css/site.css"` — the workflow fails if it cannot find it.
- „Zurück“ keeps the scroll position through the inline script at the end of `index.html` (feat/back-keeps-scroll):
  an id per history entry in `history.state`, applied again on every resize after back/forward. With manual
  `scrollRestoration` neither the browser nor Blazor scrolls on popstate: the script itself sends an entry without a
  position to the top, or it opens where the page before was scrolled (review feat/back-keeps-scroll). Never replace
  `history.state` without spreading it — Blazor reads `_index` and `userState` from it — and never scroll a page to the
  top on load yourself (`window.scrollTo(0, 0)`), or the restored position is lost.
- `Pages/News.razor` („Neuigkeiten“) is hand-written for players, not the CHANGELOG: a new section goes on top as
  `<h2>Version X.Y.Z…</h2>` inside `.news` — the bUnit test parses that heading. At 375 px the menu wraps to a second
  line; links never break inside (`nowrap`, WP-10).
- Contrast (WCAG AA 4.5 : 1): never fade text that carries information with `opacity`, and `--accent` on white is
  only ≈ 4.4 : 1 — fine for links, not for small status text. Measure light and dark in the preview
  (feat/battle-end-state).
- Colours nobody chose for readability (chat colours, type colours as text) go through `Tint.Style` + class `tinted`,
  never straight into `color`: one colour for both themes, darkened until it reads on white, like the „Schaden“ bars
  (feat/damage-text-white). `Tint.TypeColors` and the light surface mirror site.css; `TintTests` fails if they drift
  (feat/fight-log-colours). Yellow/gold as text turns brown at 4.5 : 1 — use it as a background with dark text
  (`.log .critical`, review feat/fight-log-boss-name-crit).
- Names in the chat colour: fight page, statistics, leaderboard and trainer heading (feat/leaderboard-profile-colours).
  On a link the tint goes on the `<a>` itself, or the underline stays `--accent`; `.tinted` sets weight 600, so
  `h1.tinted` restores 700. The leaderboard's 24 px pictures load lazily inside an always-rendered
  `span.profile-image` (the circle) and hide on an error; rows carry `@key` — an element hidden outside Blazor's view
  must not move to another trainer when the poll reorders rows (review feat/leaderboard-profile-colours). The sample
  data has colours but no pictures (`RepositoryTests` refuses image files under `src/`).
- Leaderboard trophies (ranks 1–3) are an own inline SVG (`Shared/Trophy.razor`), never emoji (they differ per system)
  or image files; the stroke-only handles take the metal on the dark page (claude/ranking-trophy-icons-458526).
- A fighter row is `.who` (species + owner, wraps, `flex: 1 1 0`, never `min-width: 0` — the name would cover the
  HP text) next to `.end-state` (never shrinks): never put a new element straight into `.fighters li`, or long
  names push the HP bar onto its own line again (a bUnit test checks the children) (fix/fighter-row-wrap).
  The fight cards stay inside the page's 868 px content: `.sides` reaching past it looked lopsided next to the
  narrower header and log, user decision „Breitenänderung wieder Rückgängig“ (fix/fight-card-width).
- HP bars are one component, `Shared/HpBar.razor` (fight cards and every hit in the log, review 1.8.1 B1): the numbers
  sit inside `.hp-bar` (fixed width) as two copies, each clipped to its part (`clip-path`; never `background-clip:
  text`, the numbers turn thin and grey — fix/hp-text-crisp). Fill and text colour share one hard edge at `--hp`: no
  horizontal padding or border on `.hp-bar` (feat/hp-text-in-bar). Dark fills with white text in both themes
  (fix/hp-bar-dark-mode). The log's `.hit-bar` hatches the lost piece up to `max(--before, --hp + 4px)` (`.no-loss`
  drops it), hides „HP “ for screen readers (`white-space: pre`), and its sentence needs `overflow-wrap: anywhere`
  (claude/hp-balken-kampf-log-mockup-de3cfe). Parts marked `IsTargetHp` are left out only where `TargetOf` accepts the
  hit; anything else keeps the full sentence. The damage that arrived (`Before − After`, not `LogHit.Damage`: it stays
  equal to the bar and the „Schaden“ box on a knockout) stands as „−20“ in `.hit-numbers` before the bar and the parts
  show their `TextWithBar` („ trifft “, „.“) — only when `LogHit.Damage` is set and not negative; a 1.8.1 file keeps
  the number in the sentence (claude/kampflog-hp-balken-feedback-808390, mockup „A“). Its red is lighter in the dark
  theme: `--hp-low` as text on the dark surface is under 3 : 1.
- The log has two views, „Balken“ (default) and „Text“, picked per viewer in `localStorage`
  (`ViewerSettings.LogViewKey`, shared with „Einstellungen“; `ViewerSettings` wraps every JS call in `try`/`catch
  JSException` — blocked storage means bars and a pick for this page only). The text view (`TextParts`) words a hit itself where a part is marked
  `IsDamage` („ und verursacht 20 Schaden.“, the damage that arrived) and colours parts marked `IsHp` and the
  target's HP from `LogHit` (`.log-hp` low at the bars' quarter, mid at half — a step the bars lack — high above;
  ≥ 5 : 1 light and dark). Files without `IsDamage` keep their wording. bUnit stays strict: `WebTestContext` plans
  only `localStorage.getItem` (null = bars) and `setItem`; a test's own `Setup` is added later and wins
  (claude/kampflog-design-options-cb7042, review).
- Light or dark is the viewer's pick on „Einstellungen“ (`Pages/Settings.razor`): „auto“ (default, follows the
  device), „hell“, „dunkel“. The inline script at the top of `index.html` sets `data-theme="light|dark"` on `<html>`
  before the first paint (Blazor starts too late — the page would flash), keeps the pick (`pokearena.aussehen`) and
  follows the device's `change` while on „auto“ and other tabs' picks (`storage` event); the page only calls
  `pokeArenaTheme.get` / `.set`. The values live twice, in the script and in `ViewerSettings` — a value the script does
  not know turns into „auto“ silently; `SettingsTests` ties the two (review claude/settings-page-design-00654c). Every dark rule in
  site.css hangs on `:root[data-theme="dark"]` — never `@media (prefers-color-scheme: dark)`, it would ignore „Hell“
  (`SettingsTests` checks it). No `light-dark()`: browsers before 2024 (iOS < 17.5) would lose every colour. The
  preview's colour-scheme emulation fires no `change` event: live switching is only testable on a real device. Only
  `SettingsTests` plans the two theme calls; the rest of the web tests stay strict without them. The menu's gear
  (`Shared/GearIcon.razor`, own SVG like the trophies) sits at the end of `nav`, which grows with basis `auto` — with
  basis 0 the menu squeezes in next to the brand at 375 px and wraps into three lines (claude/settings-page-design-00654c).
- „Schaden“ box (`Shared/DamageBox.razor`, `Services/DamageMeter.cs`, feat/damage-meter): a bar has the colour of
  the trainer's name (`Tint.Darkened`, a test keeps it equal to `Tint.Style`) and white text with a 1 px black
  `text-shadow` outline over the fill, `--text` over the rest, two clipped copies (feat/damage-text-white,
  feat/damage-text-outline; `-webkit-text-stroke` closes the counters at 13 px). A bar that stops short ends in a 2 px
  `--text` edge. Only colours that pass `Tint.IsColor` go into the style. No box when any fighter lacks `Damage`.
- Ranking bars are one component, `Shared/BarList.razor` with `Services/Bar.cs` (`Bar.Make` checks the colour and
  darkens it): the „Schaden“ box and the statistics page (`Pages/Stats.razor`, bars in `Services/StatisticsBars.cs`)
  share them, so a change to the bars shows on both. A bar's name can be a link; it sits in both clipped copies
  (clip-path also clips the clickable area), the copy over the fill has `tabindex="-1"`; the link's own focus ring is
  clipped, so the bar shows it (`:has(a:focus-visible)`) (feat/statistics-page).
- A new site file is missing between the release tag (the site deploys at once) and the bot's update (only after a
  stream): treat its 404 as „kommt bald“ without promising when, never as an empty game (review feat/statistics-page).
- Never tell a boss or NPC by `TrainerId == null`: a deleted trainer's fighters have no id either („[gelöscht]“). The
  boss is the fighter on side 1 of a boss fight (review feat/fight-log-colours).
