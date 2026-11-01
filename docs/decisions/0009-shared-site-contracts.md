# ADR-0009: Site data contracts in `PokeArena.Contracts`

Status: accepted · Date: 2026-11-01

## Context

The bot writes the website's JSON files (spec §9), the Blazor WebAssembly
site and the local OBS overlay read them. ADR-0001 forbids `PokeArena.Web`
from referencing Core: Core carries EF Core, SQLite and the Twitch client,
none of which belongs in a browser download. Both sides still have to agree
on every field name, and a silent mismatch only shows up as an empty page.

## Decision

- A new project `PokeArena.Contracts` (`net10.0`, no package references)
  holds the site DTOs as records, the file paths (`rangliste.json`,
  `kampf/<id>.json` …) and a `System.Text.Json` source-generated context.
- Core and Web reference it; Desktop gets it through Core. It contains no
  logic beyond path helpers.
- `meta.json` carries `FormatVersion`. A breaking change to a DTO bumps it;
  the site shows a plain "please reload" hint when it finds a newer version.

## Alternatives

- **Duplicated records in Web:** no shared project, but no compiler check
  either — a renamed field breaks the site silently. Rejected.
- **Web references Core:** pulls EF Core and the Twitch client into WASM and
  breaks ADR-0001. Rejected.

## Consequences

- The dependency rules gain: Core → Contracts, Web → Contracts.
- The source-generated context keeps the WASM build trim-safe (no reflection
  serialization).
