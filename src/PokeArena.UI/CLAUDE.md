# PokeArena.UI — pitfalls

- Razor class library, `net10.0`: no WPF/Windows references. Host specifics come in via DI (e.g. `AppInfo`).
- The audience is the streamer, not a developer: German texts, plain words, no technical jargon in the UI.
- Colors only via the tokens in `wwwroot/css/pokearena.css` (light default, dark via `prefers-color-scheme`).
  Changing `--bg` means changing `WindowTheme` in Desktop too.
- Static assets are served as `_content/PokeArena.UI/...` (see `Desktop/wwwroot/index.html`).
- Components that listen to Core events (`ITwitchConnection.StateChanged`, `ChatLog.Changed`, `DryRunMode.Changed`)
  must re-render via `InvokeAsync(StateHasChanged)` (events fire on background threads) and unsubscribe in
  `Dispose`.
- Times from Core are UTC `DateTimeOffset`; show them with `.ToLocalTime()`.
- Component tests: `tests/PokeArena.UI.Tests` (bUnit, inherit `BunitContext`, register fakes in `Services`).
- A literal `@` in Razor markup (e.g. „!fight @Name“) must be written `@@`, or Razor parses it as code.
