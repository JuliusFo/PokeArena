# PokeArena

[![CI](https://github.com/JuliusFo/PokeArena/actions/workflows/ci.yml/badge.svg)](https://github.com/JuliusFo/PokeArena/actions/workflows/ci.yml)

A persistent Pokémon collecting and battling game played through Twitch chat.
Viewers register with a starter, team up against bosses, challenge each other
and work their way through 17 NPC trainers, all via chat commands. A desktop
app on the streamer's PC runs the bot; a static website shows the leaderboard,
trainer pages and battle logs.

> **Status:** stream-ready since 1.0.0 (WP-08); see the
> [releases](https://github.com/JuliusFo/PokeArena/releases) for the current
> version. Balancing with data from real streams follows in 1.1.0. See the
> [roadmap](#roadmap).

## How it plays

All chat output is in German; the game targets casual viewers of a small
channel.

| Command | Who | Effect |
|---|---|---|
| `!registrieren <Starter>` | anyone | one-time registration with one of five starters |
| `!boss` | registered | open a boss lobby, or join one during the countdown |
| `!fight @name` | registered | challenge another trainer, or accept their challenge |
| `!challenge <NPC>` | registered | fight an NPC unlocked by an achievement |
| `!arena` | anyone | post the website link |
| `!pokearena`, `!version` | anyone | check that the bot is online, and which version runs |
| `!bossreset`, `!entfernen @name` | mods | reset the boss cooldown, delete a trainer |

- **Boss fights:** a lobby gathers participants, then the fight resolves
  instantly. Everyone whose Pokémon survives catches the boss. The boss's
  rarity and strength scale with the number of participants.
- **PvP:** two trainers fight with six random Pokémon each; the winner takes
  one Pokémon from the loser's team (the starter is protected).
- **Achievements and NPCs:** owning 15, 25 … 151 distinct species unlocks
  achievements, and each one unlocks an NPC with a fixed team.
- **Battles** use the 151 Gen-1 species with their official base stats and
  the Gen-1 type chart. Every battle is reproducible from its stored seed.

The full rules are in [docs/spec.md](docs/spec.md).

## Architecture

```
Twitch chat ──EventSub──▶ Desktop app (bot, SQLite) ──Helix──▶ Twitch chat
                                   │
                                   └─ JSON upload ─▶ Cloudflare R2 ◀─ website (GitHub Pages)
```

- The **desktop app** is a thin WPF host with a `BlazorWebView`. It holds all
  game state in SQLite, which is the single source of truth.
- The **website** is a Blazor WebAssembly app without any data of its own.
  It reads JSON files that the bot uploads to a Cloudflare R2 bucket, so
  player data never enters this repository.
- Secrets (Twitch tokens, R2 key) live only in the Windows Credential Manager.

| Project | Purpose |
|---|---|
| `src/PokeArena.Core` | Domain, battle engine, Twitch client, data, publishing. No UI, no Windows dependency |
| `src/PokeArena.UI` | Razor components and CSS of the desktop app |
| `src/PokeArena.Contracts` | Site JSON records shared by Core and Web |
| `src/PokeArena.Desktop` | WPF host: `BlazorWebView`, logging, Windows services, OBS overlay endpoint |
| `src/PokeArena.Web` | Public website, Blazor WebAssembly on GitHub Pages |
| `tests/PokeArena.Core.Tests` | Unit tests, nothing outside the process |
| `tests/PokeArena.UI.Tests`, `tests/PokeArena.Web.Tests` | bUnit component tests |
| `tests/PokeArena.Integration.Tests` | Tests against real infrastructure (Credential Manager, Twitch, R2, files) |
| `data/` | Versioned base data: species, type chart, NPCs |
| `tools/` | Dev scripts, e.g. the base-data generator |

**Tech stack:** .NET 10, Blazor Hybrid (WPF), EF Core + SQLite, Twitch
EventSub WebSocket + Helix API, Blazor WebAssembly, Cloudflare R2,
xUnit v3 on Microsoft Testing Platform.

## Running it on the streamer's PC

1. Download `PokeArena-vX.Y.Z-win-x64.zip` from the
   [latest release](https://github.com/JuliusFo/PokeArena/releases/latest)
   and unpack it, e.g. to `C:\PokeArena`. No installer and no .NET runtime are
   needed (Windows 10/11 with WebView2, which ships with Windows 11).
2. Start `PokeArena.exe` (a desktop shortcut helps). On the first start:
   Bot → **Einstellungen** (Twitch channel, website addresses) → restart the
   app → **Twitch-Verbindung** → sign in the **bot account** (not the
   streamer's own: a browser logged in to twitch.tv as the streamer confirms
   the code with the wrong account; the app then says so) → **Website** →
   enter the Cloudflare key. [docs/website-setup.md](docs/website-setup.md)
   has the details.
3. In the Twitch chat of the channel, the streamer makes the bot a moderator:
   `/mod <botname>`. Not required for chatting, but it protects the bot's
   replies against slow mode and follow-only chat (ADR-0002 addendum).
4. OBS: browser source `http://localhost:5299/overlay` — start PokeArena
   before OBS, or refresh the source once.
**Updating:** when the top bar shows „Version … verfügbar“, close PokeArena,
download the new ZIP and replace the program folder with its contents. Game
data, settings and backups live in `%LOCALAPPDATA%\PokeArena` and are not
touched; the database is backed up before the new version upgrades it.

**Restoring a backup:** close PokeArena, copy the wanted file from
`%LOCALAPPDATA%\PokeArena\backups` (the „Sicherung“ card shows the folder)
over `%LOCALAPPDATA%\PokeArena\pokearena.db`, delete `pokearena.db-wal` and
`pokearena.db-shm` next to it if they exist, and start PokeArena again. Then
„Alles neu veröffentlichen“ in the Website card brings the site in line. An
older backup may still contain trainers deleted since then (spec §11) —
delete them again.

**Releasing (developer):** set `<Version>` and the CHANGELOG section on the
package branch, merge, then tag `vX.Y.Z` on `main` and push the tag. The
[release workflow](.github/workflows/release.yml) builds the ZIP and attaches
it to the GitHub release, with the changelog section as notes.

## Building

Requirements: Windows 10/11 and the .NET 10 SDK (version pinned in
[global.json](global.json)).

```bash
dotnet build PokeArena.slnx
```

```bash
dotnet test tests/PokeArena.Core.Tests
```

```bash
dotnet test tests/PokeArena.UI.Tests
```

```bash
dotnet test tests/PokeArena.Web.Tests
```

```bash
dotnet test tests/PokeArena.Integration.Tests
```

```bash
dotnet run --project src/PokeArena.Desktop
```

Warnings are errors. The integration tests use the Windows Credential Manager
of the current user; the Twitch test is skipped until a bot login is stored.

To connect the bot during development, put the test channel into
`src/PokeArena.Desktop/appsettings.local.json` (git-ignored):

```json
{ "PokeArena": { "Twitch": { "Channel": "yourchannel" } } }
```

Then sign in the bot account in the app.

The game database lives in `%LOCALAPPDATA%\PokeArena\pokearena.db` and is
created or upgraded on start. After a model change, add a migration:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> --project src/PokeArena.Core --startup-project src/PokeArena.Core --output-dir Data/Migrations
```

`data/pokemon.json` is generated from
[PokeAPI](https://pokeapi.co/) with `dotnet run tools/GeneratePokemonData.cs`;
don't edit it by hand.

## Roadmap

| Version | Content | Status |
|---|---|---|
| 0.1.0 | Scaffold, CI, bot answers a test command in chat | done |
| 0.2.0 | Registration, persistent data | done |
| 0.3.0 | Battle engine, `!boss` playable | done |
| 0.4.0 | PvP `!fight` | done |
| 0.5.0 | NPC fights `!challenge` | done |
| 0.6.0 | Public website and OBS overlay | done |
| 0.7.0 | Commands only while the stream is live | done |
| 1.0.0 | Stream-ready: mod commands, deletion, backup | done |
| 1.1.0 | Balancing with real stream data | planned |

Details per work package: [docs/roadmap.md](docs/roadmap.md) ·
log: [docs/work-packages.md](docs/work-packages.md) ·
changes: [CHANGELOG.md](CHANGELOG.md).

## Documentation

| File | Content |
|---|---|
| [docs/spec.md](docs/spec.md) | Game rules, commands, configuration defaults |
| [docs/architecture.md](docs/architecture.md) | Data model, game flows, publishing, desktop UI |
| [docs/decisions/](docs/decisions/) | Architecture decision records |
| [docs/backlog.md](docs/backlog.md) | Unplanned ideas |

## License and disclaimer

No license is granted: the code is public for reference, but all rights are
reserved. This is a non-commercial fan project and is not affiliated with,
endorsed or sponsored by Nintendo, Game Freak, Creatures or The Pokémon
Company. Pokémon and all related names are trademarks of their respective
owners.
