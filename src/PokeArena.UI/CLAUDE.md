# PokeArena.UI — pitfalls

- Razor class library, `net10.0`: no WPF/Windows references. Host specifics come in via DI (e.g. `AppInfo`).
- The audience is the streamer, not a developer: German texts, plain words, no technical jargon in the UI.
- Colors only via the tokens in `wwwroot/css/pokearena.css` (light default, dark via `prefers-color-scheme`).
  Changing `--bg` means changing `WindowTheme` in Desktop too.
- Static assets are served as `_content/PokeArena.UI/...` (see `Desktop/wwwroot/index.html`).
