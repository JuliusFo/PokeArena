# Fight card width reverted — user report (2026-11-10, branch fix/fight-card-width)

- Built: the `@media (min-width: 1100px)` rule with `.sides { margin-inline: -70px }`
  from fix/fighter-row-wrap is gone; the cards are 868 px wide again like header
  and log. The wrap fix (`.who` with `flex: 1 1 0`) stays. CHANGELOG,
  „Neuigkeiten“ 1.4.2.
- User decisions: „Mach die Breitenänderung wieder Rückgängig, so dass wir nur
  den besseren Break haben“ — the wider cards next to the narrower log looked
  lopsided.
- Pitfalls: widen the fight cards only together with header and log, never
  `.sides` alone.
