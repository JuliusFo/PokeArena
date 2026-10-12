# ADR-0008: No official Pokémon assets

Status: accepted · Date: 2026-10-12

## Context

PokeArena is a non-commercial fan project built on the intellectual property
of Nintendo, Game Freak, Creatures and The Pokémon Company, who are known to
act against fan projects (takedown notices, cease-and-desist letters). The
repository and the website are public.

What the project uses so far carries comparatively low risk: species names,
Gen-1 base values and the type chart as text and numbers (ADR-0005). The most
direct exposure would come from images and sound — sprites, artwork, cries —
which are protected by copyright and are the usual trigger for takedowns.
Both the website and the OBS overlay (WP-06) invite adding them.

User decision on 2026-10-12: record "no official assets" as an invariant.

## Decision

No official Pokémon assets in the repository, the desktop app, the website or
the overlay — neither committed nor fetched or hot-linked at runtime. This
covers:

- sprites, artwork, 3D models and other depictions of the species, including
  fan-made drawings (they reproduce the protected designs);
- cries, music and sound effects;
- the Pokémon logo, official fonts, and logos or wordmarks imitating the
  official branding;
- screenshots, card images and other material taken from the games, anime
  or trading card game.

Allowed: species names, base values and the type chart as text or data;
generic visuals of our own (text, type colours, neutral icons, a project logo
that does not resemble the official branding). Image and sound files in the
repository must be our own work or come under a licence that permits use;
the source belongs in the commit message.

`tools/GeneratePokemonData.cs` fetches only data from PokeAPI, never sprite
URLs. Base data never stores image URLs.

## Alternatives

- **Own monster designs instead of Pokémon:** removes the risk entirely, but
  also the game's appeal for its audience. Rejected for now.
- **Private repository:** reduces visibility, but GitHub Pages from a private
  repository needs a paid plan, so the website would move (ADR-0003). Not
  decided; revisit if the risk assessment changes.

## Consequences

- Website and overlay present species as text (name, types, stats), styled
  with CSS — WP-06 designs around this.
- The README's disclaimer stays; whether the website shows one too is decided
  in WP-06.
- This lowers the risk but does not remove it: names remain trademarks. A
  takedown would still mean renaming or ending the public project.
