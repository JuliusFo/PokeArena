# „Statistik“ page — user idea (2026-11-11, branch feat/statistics-page)

- Built:
  - **Core:** `statistik.json` (`SitePaths.Statistics`, `Statistics` and its
    row records in Contracts), `SiteGenerator.StatisticsAsync` with:
    - the community Pokédex and totals
    - boss participations
    - PvP fights with wins
    - the longest boss streak without a catch
    - species only one trainer owns (count plus ten, rarest first)
    - rivals
    - the bosses that appeared and escaped most often
  - **Ranking:** top 10 per list, values above zero only, equal values share
    a place.
  - **Publishing:** queued through `SiteOutbox.ForTrainers`, so with every
    trainer change and every fight. It reads only participant rows, battle
    headers and owners, never a log.
  - **Website:** `/statistik` (`Pages/Stats.razor`, polling) with the
    damage box's bars, extracted into `Shared/BarList.razor` + `Services/Bar.cs`
    (a bar can link its name); `Services/StatisticsBars.cs`; a „Statistik“
    item in the menu; sample data.
  - **Tests:** `StatisticsTests` (every list, ties, deletion, render),
    `StatisticsBarsTests`, bUnit page and menu, and the exact-path
    assertions of the game tests updated.
  - **Docs:** spec §9 note ⁵, architecture, Web pitfalls, CHANGELOG,
    „Neuigkeiten“ 1.7.0.
- User decisions: the idea itself (screenshot: boss participations, most
  fights, lifetime damage meter, „Unglücksraben“). After the evaluation:
  - „Unglücksraben“ as a streak („Finde ich gut“)
  - fights = „PvP“
  - lifetime damage „erstmal weg“
  - added: community Pokédex, „Einzelstücke“, „Erzrivalen“ and the boss
    record („Ich würde noch die Boss-Bilanz mit aufnehmen“)
  - „Vor wp-09“, so 1.7.0
  - „Eine begrenzte Liste“ for „Einzelstücke“
  - at the plan: bars instead of tables („Ich würde Balken bevorzugen“)
- Addenda: Proposal (not confirmed) points in roadmap.md (wins next to PvP
  fights, two boss lists, rivals' leader first, colours, ties by name).
- Pitfalls:
  - `ForTrainers` changed, so every test that lists the exact queued paths
    or counts uploads had to grow by one file.
  - The name in a bar is rendered inline: a separate `RenderFragment`
    introduced whitespace between name and detail.
- Deliberately not done (→ backlog): lifetime damage, „Pioniere“,
  „Erstbesieger“, round records, PvP win streak, most loot.
- Code review (2026-11-11): „Alle beheben“ for the first pass; for the second
  pass „Ja, beide“.
  - L1 fixed: a missing `statistik.json` (right after the release tag) no
    longer says the game is empty; it gets its own text and a bUnit test.
  - L2 fixed: a chat colour change alone queues `statistik.json`. The new
    test fails without the fix.
  - L3 fixed: keyboard focus on a bar link outlines the whole bar
    (`:has(a:focus-visible)`), because the link's own ring is clipped. Not
    seen in a browser: the preview window was hidden.
  - L4 fixed (docs): ties between rivals are ordinal by Twitch id.
  - L5 fixed (docs): spec §9 and `PollingPage` list the statistics among the
    polling pages.
  - L6 fixed (second pass): the missing-file text promises no time („erscheint
    bald“), because the bot may install 1.7.0 only after a stream.
  - L7 fixed (second pass): architecture.md names the statistics as showing
    the chat colour, and the colour-only enqueue.
  - Pre-existing, not changed: Home and Battle build `trainer/<id>` links
    without escaping; this is harmless for numeric Twitch ids.
