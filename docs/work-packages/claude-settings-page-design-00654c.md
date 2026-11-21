# Settings page on the website — user request (2026-11-21, branch claude/settings-page-design-00654c)

- Idea from the user: one page for a viewer's settings, for example the fight
  log's view and light / dark / system.
- Built: `Pages/Settings.razor` (`/einstellungen`) with two switches
  (`.view-switch`, `aria-pressed`, labelled by their headings): „Aussehen“
  („Automatisch“ default, „Hell“, „Dunkel“) and „Kampfverlauf“ („Balken“ /
  „Text“, the same `localStorage` key as the switch over the fight log, which
  stays). `Services/ViewerSettings.cs` holds the keys and wraps every JS call
  (blocked storage, a cached `index.html` without the script); the fight page
  uses it too. An inline script at the top of `index.html` sets
  `data-theme="light|dark"` on `<html>` before the first paint, stores the pick
  (`pokearena.aussehen`, removed on „Automatisch“) and follows the device's
  `change` while on „Automatisch“. The three dark blocks in site.css moved from
  `@media (prefers-color-scheme: dark)` to `:root[data-theme="dark"]`. The
  menu ends in a gear (`Shared/GearIcon.razor`, own SVG, `aria-label`
  „Einstellungen“), pushed to the right; `nav` grows with basis `auto`.
  Privacy page, spec §9, CHANGELOG, „Neuigkeiten“ (under 1.8.3, proposal).
  Tests: bUnit for defaults, earlier picks, an unknown stored value, both
  switches, missing script and blocked storage, the gear in the menu, and a
  file test that site.css has no `prefers-color-scheme` media rule left and
  the script sits in `<head>`. Checked in the preview: light, dark, „Hell“ on
  a dark device, „Automatisch“ on a dark device, the pick after a reload, the
  log colours and the trophy handles under both picks, 375 px (menu in two
  lines below the brand, gear at the end of the second, no horizontal scroll),
  no console errors.
- User decisions: „Ja, für 1.8.3“ on the proposal (own page, the log switch
  stays over the log, gear in the menu, simplest variant).
- Addenda: `data-theme` set by the script instead of the proposed CSS
  `light-dark()` — browsers before 2024 (iOS < 17.5) would lose every colour
  of the page. Version 1.8.3 is a proposal (not confirmed).
- Pitfalls: the first header CSS used `flex: 1` on `nav`; at 375 px the menu
  then stayed next to the brand in three lines — basis `auto` keeps it on its
  own line below. The preview's colour-scheme emulation fires no `change`
  event (not even for a fresh listener), so switching the device while the
  page is open was not testable there.
- Deliberately not done (→ backlog): nothing.
- Code review (2026-11-21): „Alle“.
  - L: a look picked in another tab did not reach tabs already open (read
    once at load) — fixed, the script listens to `storage` (the key or a
    cleared storage) and applies again; checked with two tabs in the preview.
    Accepted limit: an open „Einstellungen“ page there keeps its pressed
    button until it is opened again (a click sends what the viewer sees).
  - L: the theme values and the `pokeArenaTheme.get` / `.set` names lived in
    the script and in `ViewerSettings` with no test tying them (bUnit fakes
    both calls) — fixed, a file test; a renamed value in the script fails it.
  - L: two test comments still named `prefers-color-scheme` and only the
    fight page — fixed.
  - Second pass: all three covered, nothing new.
