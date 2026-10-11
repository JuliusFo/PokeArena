# ADR-0007: Blazor Hybrid for the desktop UI

Status: accepted · Date: 2026-10-11 · Supersedes the UI part of ADR-0001
(WPF-UI + CommunityToolkit.Mvvm)

## Context

ADR-0001 planned the desktop app as WPF with WPF-UI and CommunityToolkit.Mvvm,
like DigitalDeskCycle2. In FerretSharp the user replaced exactly that
combination with Blazor Hybrid (FerretSharp ADR-0004, 2026-10-01) because the
WPF look felt dated and inconsistent. PokeArena's website (Blazor WebAssembly)
and OBS overlay are web pages anyway.

User decision on 2026-10-11: Blazor Hybrid, as in FerretSharp.

## Decision

- `PokeArena.Desktop` is a thin WPF host: Generic Host, Serilog, global
  exception handlers, single-instance guard, native window chrome following
  the Windows theme, and a `BlazorWebView`.
  TFM `net10.0-windows10.0.19041.0` — `BlazorWebView` needs the WinRT
  projection and crashes at startup with plain `net10.0-windows`.
- `PokeArena.UI` is a Razor class library (`net10.0`) with all components and
  the CSS design tokens (light/dark via `prefers-color-scheme`). It references
  Core but nothing Windows-specific.
- Component tests use bUnit once components contain logic.

Deliberately **not**: WPF-UI, CommunityToolkit.Mvvm, WinUI 3. Whether the
website reuses components or styles from `PokeArena.UI` is decided in WP-06
(it must not pull Core into WebAssembly).

## Consequences

- One modern look across desktop app, website and overlay, styled with CSS.
- Depends on the WebView2 runtime (preinstalled on Windows 10/11) and uses
  more memory than plain WPF: the empty shell measured ≈ 195 MB working set
  for the app process (Debug), plus the WebView2 processes — acceptable next
  to OBS on a streaming PC, but worth watching.
- UI tests move from ViewModels to components (bUnit).
