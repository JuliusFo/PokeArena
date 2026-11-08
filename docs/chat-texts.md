# Chat texts (wording pass, WP-08)

Every message the bot writes into the Twitch chat (spec §14). Placeholders in
`{braces}`. Links („Zum Kampf: …“) are appended to fight results when the
website is set up.

Reviewed 2026-11-04: the user accepted all six proposals (rows 2, 4, 10, 16,
28, 29); the texts below are the ones in the code. A later change of wording
goes into this table and the code together. The **Proposal** and **Change**
columns stay for the next review.

## Registration and status

| # | Situation | Text | Proposal | Change |
|---|---|---|---|---|
| 1 | `!registrieren` without or with an unknown starter | @{Name}, wähle deinen Starter: Bisasam, Schiggy, Glumanda, Pikachu oder Evoli – zum Beispiel: !registrieren Pikachu | | |
| 2 | Registered | @{Name}, willkommen bei PokeArena! Dein Starter ist {Starter}. Mit !boss kämpfst du mit, sobald ein Boss erscheint. | | |
| 3 | Already registered | @{Name}, du bist schon dabei – dein Starter ist {Starter}. | | |
| 4 | Game command while not registered | @{Name}, du bist noch nicht dabei. Melde dich an, zum Beispiel mit !registrieren Pikachu. | | |
| 5 | `!pokearena`, `!version` | PokeArena-Bot ist online (Version {Version}). | | |
| 6 | `!arena` | Rangliste, Kämpfe und Trainer: {Website} | | |
| 7 | `!arena`, website not set up | Die PokeArena-Website ist noch nicht eingerichtet. | | |
| 8 | Command while the stream is offline (at most every 5 min) | PokeArena läuft nur, solange der Stream live ist. Bis zum nächsten Stream! | | |

## Boss (`!boss`)

| # | Situation | Text | Proposal | Change |
|---|---|---|---|---|
| 9 | Lobby opens | Ein wilder Boss nähert sich! Schreibt !boss, um mitzukämpfen – noch 60 Sekunden. | | |
| 10 | Too few joined | Der Boss entkommt – es haben sich zu wenige Trainer getraut. Mindestens 2 müssen mit !boss mitmachen. | | |
| 11 | Fight starts | Ein wildes {Boss}{ (selten) / (legendär)} erscheint! {N} Trainer kämpfen mit. | | |
| 12 | Boss defeated | {Boss} wurde besiegt! Gefangen von: {A, B}. | | |
| 13 | Boss escaped, some survived | {Boss} ist entkommen. | | |
| 14 | Boss escaped, all fainted | {Boss} ist entkommen – alle Pokémon wurden besiegt. | | |
| 15 | `!boss` during the cooldown (at most every 30 s) | @{Name}, der nächste Boss kann in {X Minuten} gerufen werden. | | |
| 16 | Achievement reached (boss, PvP, NPC) | {Name} hat {15} verschiedene Pokémon gesammelt und kann jetzt {Eich} mit !challenge {Eich} herausfordern! | | |
| 39 | Boss cooldown after a fight has run out | Der Boss ist wieder bereit – schreibt !boss, um ihn zu rufen! | | |

## PvP (`!fight`)

| # | Situation | Text | Proposal | Change |
|---|---|---|---|---|
| 17 | Without a name | @{Name}, fordere jemanden zum Kampf heraus – zum Beispiel: !fight @Name | | |
| 18 | Unknown name | @{Name}, {x} ist noch kein Trainer. | | |
| 19 | Challenging oneself | @{Name}, du kannst dich nicht selbst herausfordern. | | |
| 20 | Own challenge still open | @{Name}, du hast schon eine offene Herausforderung. | | |
| 21 | Target has an open challenge | @{Name}, {Target} hat schon eine offene Herausforderung. | | |
| 22 | Too few Pokémon | @{Name}, {du hast / Misty hat} erst {n} Pokémon – für einen Kampf braucht ihr beide mindestens 6. | | |
| 23 | Challenge | @{Target}, {Name} fordert dich zum Kampf heraus! Nimm an mit !fight @{login} – du hast 2 Minuten. | | |
| 24 | Accepted, but someone lost Pokémon meanwhile | Der Kampf fällt aus: {Misty hat} erst {n} Pokémon – für einen Kampf braucht ihr beide mindestens 6. | | |
| 25 | Result | {Winner} besiegt {Loser} und bekommt {Pokémon} von {Loser}! | | |

## NPC (`!challenge`)

| # | Situation | Text | Proposal | Change |
|---|---|---|---|---|
| 26 | Without/unknown NPC, nothing unlocked | @{Name}, fordere einen NPC heraus. Den ersten (Eich) schaltest du mit 15 verschiedenen Pokémon frei. | | |
| 27 | Without/unknown NPC, some unlocked | @{Name}, fordere einen NPC heraus – zum Beispiel: !challenge {Eich}. Freigeschaltet: {Eich, Rocko}. (unknown name: „einen NPC namens {x} gibt es nicht“) | | |
| 28 | Not unlocked yet | @{Name}, für {NPC} brauchst du noch {n} verschiedene Pokémon. | | |
| 29 | Cooldown | @{Name}, gegen {NPC} kannst du in {1 Stunde 30 Minuten} wieder antreten. | | |
| 30 | Too few Pokémon | @{Name}, du hast erst {n} Pokémon – für einen Kampf brauchst du mindestens 6. | | |
| 31 | First win | {Name} besiegt {NPC} und bekommt den Orden von {NPC}, {Pokémon} und {n} Tokens! | | |
| 32 | Later win | {Name} besiegt {NPC} erneut – eine Belohnung gibt es nur beim ersten Sieg. | | |
| 33 | Lost | {NPC} besiegt {Name}! Nächster Versuch in {48 Stunden}. | | |

## Moderators

| # | Situation | Text | Proposal | Change |
|---|---|---|---|---|
| 34 | `!bossreset` | Der Boss-Cooldown ist zurückgesetzt – mit !boss geht es sofort weiter. | | |
| 35 | `!entfernen` without a name | @{Mod}, wen soll ich löschen? Zum Beispiel: !entfernen @name | | |
| 36 | `!entfernen` unknown name | @{Mod}, ich finde keinen Trainer namens {x}. | | |
| 37 | `!entfernen` asks for confirmation | @{Mod}, wirklich {Name} mit allen Pokémon löschen? Das lässt sich nicht rückgängig machen. Bestätige innerhalb von 60 Sekunden mit !entfernen @{login} ja | | |
| 38 | Deleted | {Name} wurde mit allen Pokémon gelöscht. | | |
