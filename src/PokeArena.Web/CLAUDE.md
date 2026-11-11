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
- `Pages/News.razor` („Neuigkeiten“) is hand-written for players, not the CHANGELOG: a new section goes on top as
  `<h2>Version X.Y.Z…</h2>` inside `.news` — the bUnit test parses that heading. At 375 px the menu already wraps to
  a second line with four items; links never break inside (`nowrap`, WP-10).
- Contrast (WCAG AA 4.5 : 1): never fade text that carries information with `opacity`, and `--accent` on white is
  only ≈ 4.4 : 1 — fine for links, not for small status text. Measure light and dark in the preview
  (feat/battle-end-state).
- Colours nobody chose for readability (chat colours, type colours as text) go through `Tint.Style` + class `tinted`,
  never straight into `color`. `Tint.Style` gives one colour for both themes, darkened until it reads on white — the
  same as the „Schaden“ bars: user decision „die Balken-Farbe zum Standard“, a test whether names stay readable on
  the dark page, where dark colours (blue) fall below 4.5 : 1 (feat/damage-text-white; until 1.6.0 a lighter dark-theme
  variant). `Tint.TypeColors` and the light surface mirror site.css; `TintTests` fails if they drift
  (feat/fight-log-colours). Yellow/gold as text turns brown on white at 4.5 : 1 — use it as a background with dark
  text (`.log .critical`, review feat/fight-log-boss-name-crit).
- Names in the chat colour: fight page, statistics, leaderboard and trainer heading (feat/leaderboard-profile-colours).
  On a link the tint goes on the `<a>` itself, or the underline stays `--accent`; `.tinted` sets weight 600, so
  `h1.tinted` restores 700. The leaderboard's 24 px pictures load lazily and hide with `visibility` (not `display`)
  on an error, and rows without one get an empty `span.profile-image`, so the names stay aligned. The sample data
  has colours but no pictures (`RepositoryTests` refuses image files under `src/`).
- A fighter row is `.who` (species + owner, wraps, `flex: 1 1 0`, never `min-width: 0` — the name would cover the
  HP text) next to `.end-state` (never shrinks): never put a new element straight into `.fighters li`, or long
  names push the HP bar onto its own line again (a bUnit test checks the children) (fix/fighter-row-wrap).
  The fight cards stay inside the page's 868 px content: `.sides` reaching past it looked lopsided next to the
  narrower header and log, user decision „Breitenänderung wieder Rückgängig“ (fix/fight-card-width).
- The HP numbers sit inside `.hp-bar` (fixed width): bar fill and `.hp-text` colour share one hard gradient edge at
  `--hp`. No horizontal padding or border on `.hp-bar` itself, or the edges drift apart; the fills are darker
  than `--ok`/`--accent` because white text on those is under 4.5 : 1 (feat/hp-text-in-bar). Both themes use the
  same dark fills with white text: bright `--ok` with dark text glared in the dark theme despite 8 : 1, user
  decision variant „B“ (fix/hp-bar-dark-mode). The numbers are two stacked copies, each clipped to its part
  (`clip-path`, not overlapping, or the dark copy fringes the white one); never `background-clip: text` — it loses
  subpixel antialiasing and the numbers looked thin and grey (fix/hp-text-crisp).
- „Schaden“ box (`Shared/DamageBox.razor`, logic in `Services/DamageMeter.cs`, feat/damage-meter): a bar has the
  colour of the trainer's name (`Tint.Darkened`, the colour of `Tint.Style`; a test keeps them equal; yellow turns
  olive) and white text over the fill like the HP numbers, in both themes like the HP bar's dark fills — user requests
  „Text-Farbe wie bei den HP“, „die abgedunkelte Version, wie vorher die Namen“, „die Balken-Farbe zum Standard“
  (feat/damage-text-white; tried and dropped: raw colours everywhere). The white copy has a 1 px black outline
  (`text-shadow` in eight directions; user pick „1B“ over 1.5 px, 2 px and `-webkit-text-stroke`, which closes the
  counters at 13 px — feat/damage-text-outline). `--text` over the rest, two clipped copies. A bar that stops short
  ends in a 2 px `--text` edge (review feat/damage-meter). Only colours that pass `Tint.IsColor` go into the inline
  style. No box when any fighter lacks `Damage` (older files).
- Ranking bars are one component, `Shared/BarList.razor` with `Services/Bar.cs` (`Bar.Make` checks the colour and
  darkens it): the „Schaden“ box and the statistics page (`Pages/Stats.razor`, bars in `Services/StatisticsBars.cs`)
  share them, so a change to the bars shows on both. A bar's name can be a link; it sits in both clipped copies
  (clip-path also clips the clickable area), the copy over the fill has `tabindex="-1"`; the link's own focus ring is
  clipped, so the bar shows it (`:has(a:focus-visible)`) (feat/statistics-page).
- A new site file is missing between the release tag (the site deploys at once) and the bot's update (only after a
  stream): treat its 404 as „kommt bald“ without promising when, never as an empty game (review feat/statistics-page).
- Never tell a boss or NPC by `TrainerId == null`: a deleted trainer's fighters have no id either („[gelöscht]“). The
  boss is the fighter on side 1 of a boss fight (review feat/fight-log-colours).
