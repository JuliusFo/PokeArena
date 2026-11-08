# Gesamt-Review 1.0.3 (2026-11-08)

Auftrag des Nutzers (2026-11-08): „Mach mal ein Code-Review auf unseren bisherigen Code. Du kannst dabei ja die Reviews von FerretSharp als Anleitung nehmen, musst sie aber nicht 1:1 übernehmen.“ Erstes Gesamt-Review über die ganze Solution (WP-00 bis WP-08, Releases bis 1.0.3), nach dem Muster von FerretSharp `complete-code-review`, verkleinert auf sechs parallele Agenten nach Themen. Alle Agenten haben nur gelesen, und zwar auf einer festen Kopie von `a2fdf4f` (= `main`):

- GL: Spiellogik und Daten
- TW: Twitch, Chat-Queue, Nebenläufigkeit, Host, Build/CI
- WE: Publishing, Website, Overlay, Desktop-UI
- SE: Sicherheit und Datenschutz
- TE: Tests; als einziger Agent mit Build und Tests, dazu Mutationstests auf einer Kopie
- RD: Wiederverwendung, toter Code, Struktur, Doku

**Stand:** in Arbeit mit R1 (`wp/r1-review-1.0.3`).

Doppelte Befunde sind zusammengeführt (alle IDs genannt). **Nachgeprüft** heißt: zusätzlich von Hand im Code bestätigt; sonst gilt die Einschätzung des Agenten.

Schwere (wie `.claude/skills/branch-code-review/checklist.md`): **S** falsche oder verlorene Spieldaten, gebrochene Invariante, Sicherheits- oder Datenschutzlücke, Bot hängt, antwortet doppelt oder gar nicht · **M** falsches Verhalten in realistischen Fällen oder klare Wartungslast · **K** Kleinkram.

Build und Tests auf dem Snapshot:
- `dotnet build -warnaserror` ohne Warnung.
- 233 Tests grün: Core 192, UI 30, Web 11. Ebenso unter de, en-US, tr-TR und invariant sowie in 8 parallelen Läufen.
- Integrationstests nur gelesen. Coverage: kein Paket referenziert.
- Keine anfälligen Pakete; veraltet sind nur Patch-Stände (EF Core/Extensions 10.0.14, AWSSDK.S3, WebView.Wpf, CredentialManager, bunit, TimeProvider.Testing).

## Ohne Befund geprüft (Auszug)

- **Invarianten:**
  - Schlüssel nur per Twitch-Id, R2-Pfade nur aus der numerischen Id.
  - Keine Artwerte in der DB.
  - Kein Zufall und keine Uhr in Core außerhalb der Schnittstellen. Ausnahme: ein Echtzeit-CTS, siehe A24.
  - Starter nie Beute: Filter plus gefilterter Unique-Index.
  - Achievements bleiben bei Artverlust.
  - Ein `BattleRandom(seed)` je Begegnung in fester Reihenfolge; Seed und volles Log gespeichert.
- **Spielregeln:**
  - Boss: Stufen, HP 1,5 × N, Ziele ⌈N/3⌉, Fänger, Cooldowns, „Boss bereit“.
  - Schadensformel, Team-Kampf, PvP, NPC: gegen spec §4–§7 und ADR-0006 geprüft.
  - Alle 17 Defaults aus §13 stimmen mit dem Code überein, ebenso `npcs.json` mit §7 und der Anleitung.
- **Transaktionen:**
  - Boss, PvP, NPC und Löschung laufen in `BEGIN IMMEDIATE`, zusammen mit der Outbox.
  - Die Löschung entfernt alles (inkl. Präfix-Grenzfall der NPC-Cooldowns), anonymisiert die Logs und reiht alle betroffenen Dateien ein.
  - Die UI-Löschung läuft über die Queue.
- **Twitch:**
  - Refresh serialisiert und sofort gespeichert.
  - `session_reconnect` verbindet erst neu, dann schließt er die alte Verbindung.
  - Keepalive über `TimeProvider`.
  - Eigene Nachrichten und Shared-Chat ignoriert, U+E0000 entfernt.
  - Frames auf 1 MB begrenzt, Duplikatfilter.
  - Backoff 2–60 s.
  - `ChatText.Split` hält 500 Zeichen ein.
- **Command-Fluss:**
  - Ein Leser der `ChatInbox`; Timer posten nur `QueuedWork`.
  - Live-Gate vor der ersten Nachricht; Mod-Befehle umgehen es gewollt.
- **Sicherheit:**
  - Mod-Rolle nur aus den Badges der Nachricht.
  - Bestätigung beim Löschen: derselbe Mod, dieselbe Id, derselbe Modus, 60 s.
  - Kein `MarkupString`, Overlay nur über `textContent`, Kestrel nur auf Loopback.
  - Secrets nur im Credential Manager und nirgends geloggt; EF Core ohne SQL-Logging.
  - Keine Tokens oder Spielerdaten in der Git-Historie.
- **Publishing:**
  - Revisionsfeld fängt erneutes Einreihen während des Uploads ab.
  - Backoff begrenzt, Neustart verliert nichts.
  - `OpenLive()` nur in Worker, Republish und Backup.
  - Cache-Header 10 s / 1 Tag, 404 nicht gecacht.
- **Struktur und Doku:**
  - Schichtregeln §5 eingehalten.
  - Zentrale Paketversionen, `nuget.config` nur nuget.org.
  - ADR 0001–0009 lückenlos.
  - Alle relativen Links lösen auf, CHANGELOG-Abschnitte = Tags.
  - Alle CSS-Klassen benutzt, Backlog ohne Erledigtes.
  - `docs/chat-texts.md` vollständig in beide Richtungen, bis auf D8.
- **Tests:**
  - Keine festen Wartezeiten außer C12, keine echte Uhr.
  - Kämpfe per Seed vorhergesagt.
  - Kein Test langsamer als 0,1 s, außer dem Aufwärmen je Assembly.

## Teil A – Fehler und Sicherheit

### Schwer

Keine.

### Mittel

| # | IDs | Ort | Befund | Sicherheit |
|---|---|---|---|---|
| A1 | TW4 | `Core/Twitch/TwitchConnection.cs:283`, `Core/Twitch/EventSub/ChatMessageEvent.cs:24` | **Login mit dem Streamer- statt dem Bot-Account wird nicht erkannt.**<br>Szenario: Auf dem PC des Streamers ist twitch.tv mit seinem Account angemeldet. Beim Geräte-Login (Ersteinrichtung oder nach 30 Tagen Inaktivität) bestätigt er deshalb mit seinem eigenen Account. Dann gilt `BotUserId == BroadcasterId`.<br>Folgen: Jede Nachricht des Streamers wird als „eigene Nachricht“ verworfen; er kann weder spielen noch `!bossreset`/`!entfernen` nutzen. Der Bot schreibt als Streamer. Die UI zeigt „Verbunden“.<br>Vorschlag: In `RunSessionAsync` bei `bot.UserId == channel.Id` abbrechen, mit neuem `TwitchProblem.WrongAccount` („Bitte mit dem Bot-Account anmelden“). Test dazu. | nachgeprüft |
| A2 | TW2 | `TwitchConnection.cs:246-253` | **Jede nicht vorübergehende `TwitchApiException` (4xx außer 429) beendet die Verbindung endgültig.** Der Zustand ist dann `Failed`, ohne neuen Versuch.<br>Szenario: Nach Standby oder Netzverlust brauchen die Abos länger als die 10 s, die Twitch gewährt. Twitch schließt die Session, der laufende `POST eventsub/subscriptions` bekommt 400. Der Bot bleibt mitten im Stream offline, bis der Streamer „Erneut versuchen“ klickt.<br>Vorschlag: Nur 401, 403 und „Kanal nicht gefunden“ als endgültig behandeln; alle anderen 4xx mit dem vorhandenen Backoff wiederholen. | Pfad nachgeprüft, Auslöser (Statuscode der toten Session) Verdacht |
| A3 | TW1 | `TwitchConnection.cs:330, 398` | **Der Live-Status aus Helix `GET /streams` überschreibt jedes Mal den bisherigen Status.**<br>Szenario: PokeArena startet (oder verbindet neu) in den ersten Minuten nach dem Livegang. `stream.online` kam vor dem Abo, und Helix meldet erfahrungsgemäß noch „offline“. Folge: Der Bot verwirft eine Stunde lang alle Befehle und antwortet „läuft nur, solange der Stream live ist“. Umgekehrt nimmt er nach dem Stream-Ende noch bis zu einer Stunde Befehle an.<br>Vorschlag: Ein Event der letzten ~10 min hat Vorrang vor der Abfrage. Nach dem Start bei „offline“ einige Minuten lang alle 2 min nachfragen (oder generell alle 5 min statt stündlich). Test mit `FakeTimeProvider`. → Frage 2. | Pfad nachgeprüft, Verzögerung von Helix Verdacht |
| A4 | TW3, TW5 | `Desktop/App.xaml.cs:123-135` | **`async void OnExit`: Nach dem ersten `await` beendet WPF den Dispatcher, die Fortsetzung läuft nie.** Damit fehlen beim Beenden `_host.Dispose`, `Log.CloseAndFlushAsync`, das Leeren der Sende-Queue und das saubere Schließen des Sockets.<br>Szenario: Der Streamer schließt das Fenster direkt nach einem Boss-Kampf; Ergebnis und Achievement-Meldungen gehen verloren. Die Daten bleiben heil.<br>Vorschlag: Im `Closing` des Fensters `e.Cancel = true`, den Host per `await` stoppen, dann `Shutdown()`.<br>Achtung (TW5): Die Twitch-Schleife läuft wegen `Task.Yield` auf dem UI-Thread. Ein blockierendes Warten würde deshalb verklemmen. Also zugleich `Task.Run` statt `Task.Yield` und das Stop-Token in `TwitchStartup.StopAsync` beachten. | Mechanik nachgeprüft, WPF-Verhalten Verdacht |
| A5 | SE1 | `Core/Chat/ChatSender.cs:36`, Fehlerantworten in `CommandProcessor.cs:105`, `PvpService.cs:79/85`, `NpcService.cs:47-50`, `RegisterCommand.cs:54` | **Chat-Flut staut wichtige Bot-Nachrichten.** Die Sende-Queue hat keine Grenze. Fehler- und Hinweisantworten werden pro Nutzer nicht gedrosselt; nur `!pokearena`, `!arena`, der Boss-Cooldown und der Offline-Hinweis sind gedrosselt.<br>Szenario: Zwei Zuschauer schicken je 20 `!fight`/`!challenge xyz` pro 30 s. Das ergibt 40 Antworten pro 30 s, der Limiter schafft aber nur 20. Nach 5 min Spam sind Boss-Ergebnisse 5 min verspätet. Die 2-min-Frist von `!fight` läuft ab, bevor der Herausgeforderte die Nachricht sieht.<br>Das Drosseln steht im Backlog, die Folge für wichtige Nachrichten nicht.<br>Vorschlag: Fehler- und Hinweisantworten pro Nutzer drosseln (eine pro 30 s), dazu `ReplyThrottle` aus B3. Hinweise mit Höchstalter in der Queue verwerfen, Ergebnisse nie. Test: 20× `!fight` in 10 s → höchstens eine Antwort. → Frage 3. | nachgeprüft |
| A6 | WE1 | `Core/Publishing/SiteOutbox.cs:45-50`, `README.md:91-97` | **„Alles neu veröffentlichen“ nach dem Einspielen einer Sicherung räumt R2 nicht auf.** Die README verspricht das aber. Es werden nur Pfade eingereiht, die die DB kennt.<br>Szenario: Sicherung von gestern, heute kamen Trainer X und die Kämpfe 121–130 dazu. Folgen:<br>– `trainer/X.json` und `kampf/121…130.json` bleiben in R2.<br>– X ist nicht mehr in der DB, also löscht ihn auch `!entfernen` nicht. Seine Seite bleibt öffentlich (Datenschutz).<br>– Der nächste Kampf bekommt wieder die Id 121. Cloudflare und der Browser liefern einen Tag lang den alten Kampf 121 aus, der Chat-Link zeigt den falschen Kampf.<br>Vorschlag: Beim Republish zusätzlich `trainer/` und `kampf/` im Bucket auflisten (ListObjectsV2) und einreihen; der Worker löscht Pfade ohne Quelle von selbst. Nach dem Einspielen die Kampf-Sequenz über die höchste hochgeladene Id setzen, oder das in der README nennen. → Frage 4. | nachgeprüft |
| A7 | WE2, WE6 | `SitePublishWorker.cs` (`OrderBy(NextAttemptAt)`), `SiteOutbox.cs:33/49`, `Web/Pages/Battle.razor:72` | **Republish hält neue Kämpfe auf.** Alle Zeilen bekommen `NextAttemptAt = T0`, ein späterer Kampf `T1 > T0` und wird damit hinten einsortiert.<br>Rechnung: Bei ~300 Trainern und 1000 Kämpfen sind das ~52 Runden zu 25 Dateien, also mehrere Minuten. Die Kampfseite gibt nach 2 min auf und meldet „Diesen Kampf gibt es nicht“ (WE6: auch bei reinen Netzfehlern). Der Chat-Link ist dann kaputt.<br>Vorschlag: Fällige Zeilen mit `Attempts == 0` nach `QueuedAt` absteigend abarbeiten. Die Kampfseite unterscheidet „noch nicht da“ von „Fehler“. | nachgeprüft |
| A8 | WE3 | `UI/OverlayPanel.razor:125-127`, `Desktop/Services/OverlayServer.cs:46` | **Startet OBS vor PokeArena, bleibt das Overlay leer.** Die Overlay-Seite liefert die App selbst aus; die Browserquelle bekommt „Connection refused“ und lädt nicht von selbst neu. Die Wiederholung in `overlay.html` greift erst, wenn die Seite schon geladen ist. Das Overlay bleibt also den ganzen Stream leer.<br>Vorschlag: Mindestens ein Hinweis in der Karte („PokeArena vor OBS starten, sonst die Quelle aktualisieren“ bzw. die OBS-Option „Browser aktualisieren, wenn Szene aktiv wird“). Robuster: eine lokale HTML-Datei, die per CORS pollt. → Frage 5. | Verdacht (OBS/CEF-Verhalten) |
| A9 | SE5 | `docs/website-setup.md:38-56` | **Die Cache-Regel ignoriert den Query-String nicht.** `rangliste.json?r=<zufall>` ist dann immer ein MISS und ein bezahlter R2-Lesezugriff. Das Rate-Limit erlaubt pro IP dauerhaft ~5/s, also ~13 Mio. Lesezugriffe pro Monat. Genau dagegen sollte der Cache laut Addendum zu ADR-0003 schützen („no spending cap“).<br>Vorschlag: Im Cache-Key den Query-String ignorieren, dazu eine WAF-Regel „Query-String nicht leer oder Methode nicht GET/HEAD → blockieren“. Prüfschritt in §5: `curl -I …?x=1` zweimal → `HIT` oder 403. | Doku nachgeprüft; Live-Konfiguration nicht prüfbar |

### Klein

| # | IDs | Befund |
|---|---|---|
| A10 | TW8 | `ChatSender.cs:71` verwirft Nachrichten, solange es kein Twitch-Ziel gibt (Reconnect, 2–60 s). Ein Boss-Ergebnis in diesem Fenster erreicht den Chat nie. Begrenzt (z. B. 60 s) auf ein Ziel warten; eine nie gesendete Nachricht kann nicht doppelt ankommen. |
| A11 | TW7 | `TwitchTokenProvider.cs:87` bricht den Refresh mit dem Token des Aufrufers ab. Hat Twitch den Refresh schon ausgeführt, geht das neue Einweg-Token verloren, und der Streamer braucht einen neuen Geräte-Login. Den Request mit eigenem Timeout und ohne Aufrufer-Abbruch laufen lassen. (Verdacht) |
| A12 | TW6 | `EventSubSocket.cs:30`: `ConnectAsync` hat kein Zeitlimit. Beim Handover blockiert es die Session-Schleife samt Keepalive-Prüfung. Hängt der Upgrade, hängt der Bot unbegrenzt. Verknüpften CTS mit ~15 s über `timeProvider`. (Verdacht) |
| A13 | GL1, TW9 | Der Schalter für den Probelauf springt auf dem UI-Thread um, mitten zwischen zwei `database.Open()` eines Befehls. Ein simuliertes `!registrieren` kann so in der Live-DB samt Outbox landen. Das Fenster ist Millisekunden groß. Umschalten als `QueuedWork` über die Inbox. |
| A14 | GL2, SE6 | `TrainerService.FindByNameAsync` mit `FirstOrDefault` über einen nicht eindeutigen `Login`.<br>Szenario: A hieß „bob“, hat sich umbenannt und schreibt nicht mehr; B übernimmt später „bob“. `!entfernen @bob ja` kann A löschen.<br>Beim Speichern eines Logins denselben Login bei anderen Trainern leeren; bei mehreren Treffern im Chat nicht löschen. |
| A15 | GL3 | Löschen eines Trainers lässt seine offenen Herausforderungen im Speicher. B bekommt bis zum Timeout „du hast schon eine offene Herausforderung“. `PvpService.Forget(trainerId)` aus der Löschung aufrufen. |
| A16 | GL4 | `!challenge @Eich` → „einen NPC namens @Eich gibt es nicht“. `!fight` entfernt das `@`, `!challenge` nicht. |
| A17 | GL5 | `SpeciesCatalog.Validate` prüft nicht, ob jede Seltenheitsstufe Arten hat. Fehlt eine, wirft jede Lobby mit dieser Stufe; im Chat bleibt nur „Ein wilder Boss nähert sich!“. Fehlende `typen`/`team` geben eine `NullReferenceException` statt der lesbaren Meldung. |
| A18 | SE2 | `PvpService.cs:76/85`, `NpcService.cs:39/47` geben das Argument ungekürzt zurück. Geteilt wird nur an U+0020, ein „Wort“ kann also mit NBSP ein ganzer Satz von 480 Zeichen sein. Das kostet das doppelte Sendebudget, und das Echo bleibt stehen, wenn ein Mod die Nachricht löscht. Mention-Ping: Verdacht. Nur bei Login-Muster `^[A-Za-z0-9_]{1,25}$` zurückgeben, an `char.IsWhiteSpace` teilen. |
| A19 | SE3 | `ChatMessageEvent.cs:49` erkennt nur das Badge `moderator`. Twitchs „Lead Moderator“ trägt vermutlich `lead_moderator` und kann `!entfernen`/`!bossreset` nicht nutzen. Fail-closed. (Verdacht – Badge-Namen gegen die Twitch-Doku prüfen) |
| A20 | SE4, RD | Die Logdatei (Debug, 14 Tage) behält Anzeigename und Login bei Umbenennungen (`TrainerService.cs:138`) und den vollen Nachrichtentext bei Fehlern (`CommandProcessor.cs:113`), auch nach `!entfernen`. Die Datenschutzseite (`Web/Pages/Privacy.razor:10`) nennt weder das Log noch Login, Orden und Tokens. Nur Ids loggen, Seite ergänzen. |
| A21 | SE7, TW11 | `release.yml`: `contents: write` für den ganzen Job, `actions/checkout` legt den Token in `.git/config`. Paketcode in Build und Tests könnte damit die Release-ZIP ersetzen. `persist-credentials: false`, Upload in einen eigenen Job; optional SHA-Pins. |
| A22 | TW10 | `release.yml` prüft weder, ob Tag und `<Version>` übereinstimmen, noch ob es den CHANGELOG-Abschnitt gibt (`exit 0` → generierte Notizen). Ein früher Schritt soll in beiden Fällen fehlschlagen. |
| A23 | TW12 | `BannedSymbols.txt` sperrt `Task.Delay`, `CancellationTokenSource(TimeSpan)`, `CancelAfter` und `Stopwatch` ohne `TimeProvider` nicht. Einen Verstoß gibt es schon: `EventSubSession.cs:228`. Aufnehmen und umstellen. |
| A24 | TW13 | `global.json` `rollForward: latestFeature` plus `AnalysisLevel latest` plus Warnungen als Fehler: Ein neues SDK im Runner kann CI und Release ohne Codeänderung rot machen. `AnalysisLevel` fest auf `10.0`. (Verdacht) |
| A25 | WE4 | `SiteOutbox.EnqueueAsync` ändert vorhandene Zeilen getrackt. `RegisterAsync` und `ObserveAsync` laufen ohne Transaktion. Löscht der Worker die Zeile genau dazwischen, gibt es eine `DbUpdateConcurrencyException`: Die Registrierung geht verloren, im Chat kommt keine Antwort. Beide in `BeginTransactionAsync` einschließen. |
| A26 | WE5 | Nach Korrektur eines falschen R2-Schlüssels behalten die Zeilen ihr Backoff (bis 15 min). Die Karte zeigt weiter „Zugangsdaten abgelehnt“, und der neue Schlüssel wirkt falsch. Zudem entsorgt `SaveCredentials` den Publisher mitten im Upload. Bei `R2Access.Changed` die Wartezeiten zurücksetzen, den alten Publisher erst nach der Runde entsorgen. |
| A27 | WE7 | `Home.razor:12-15`, `Battles.razor:66-69`: Scheitert der erste Ladeversuch, steht „Noch niemand dabei“ direkt neben dem Fehlerhinweis. Leerzustand nur bei `!Failed`. |
| A28 | WE8 | `PollingPage.cs:101-121` fängt nur `HttpRequestException`. Ein HttpClient-Timeout (`TaskCanceledException`) beendet das Polling still bzw. lässt die Seite auf „Lädt …“ stehen. |
| A29 | WE9 | `overlay.html:207`: „1 Arten“, im ersten Stream bei allen fünf Einträgen. |
| A30 | WE10 | Ist Port 5299 belegt, steht das nur im Log; die Overlay-Karte gibt trotzdem die Anleitung. Status anzeigen. |
| A31 | WE11 | `TrainerService.DeleteAsync` benachrichtigt `BattleService` nicht; der Reiter „Kämpfe“ zeigt den gelöschten Namen weiter. |
| A32 | WE12 | Jede Overlay-Abfrage (alle 2 s, solange OBS läuft) lädt alle Trainer mit allen Exemplaren. Ergebnis cachen bis `Changed`. (Verdacht – nicht gemessen) |
| A33 | WE13 | `MainLayout.razor:254` prüft nur „Daten neuer als App“. Normal ist aber der umgekehrte Fall: Pages wird beim Merge deployt, der Bot erst mit dem nächsten ZIP. Beim ersten Formatwechsel liest die neue Seite dann tagelang alte Dateien. N-1 lesen oder einen Hinweis zeigen; die Reihenfolge im `/release`-Skill festhalten. → Frage 6. |
| A34 | WE14 | `Home.razor:19` „Stand 22:14 Uhr“ ohne Datum; zwei Tage später wirkt das wie heute. |

## Teil B – Struktur und Wiederverwendung

| # | IDs | Vorschlag | Aufwand |
|---|---|---|---|
| B1 | RD1 | **Kampftexte einmal:** Überschrift und Ergebnis entstehen dreimal (Chat in Boss/Pvp/NpcService, Website in `SiteGenerator.Describe`, Desktop in `BattleService` + `BattleListPanel`) und sind schon auseinandergelaufen:<br>– NPC-Erstsieg: drei verschiedene Sätze.<br>– Gelöschte Trainer: „(gelöscht)“ gegen „[gelöscht]“.<br>– Der Desktop zeigt heutige Namen, die Website die Namen zum Zeitpunkt des Kampfs.<br>– `TierSuffix` steht doppelt (UI-Kopie, weil `internal`), der Token-Plural dreifach.<br>Vorschlag: Eine Klasse `BattleTexts` in Core, die Website und Desktop nutzen. | M |
| B2 | RD2 | Zwei `BattleKind` (Data/Contracts) mit Mapping-Switch und eigener CLAUDE.md-Falle, zwei `BattleSummary` mit verschiedenem Inhalt. `Contracts.BattleKind` in der DB nutzen (Enum als Text, Werte gleich), Core-`BattleSummary` umbenennen. | S |
| B3 | RD3 | Cooldown lesen/setzen zweimal identisch (`BossEncounter`, `NpcService`), „Antwort höchstens alle X“ viermal. Neu: `Cooldowns.Get/SetAsync` und `ReplyThrottle` (Grundlage für A5). | S |
| B4 | RD4, GL6 | Deutsche Dauerformatierung viermal. `NpcService.Duration` und `HoursAndMinutes` liefern für 90 min verschiedene Texte; nach WP-09 widersprechen sich Niederlage-Meldung und Cooldown-Antwort. Eine `ChatText.Duration`, doppeltes `<summary>` in `NpcService.cs:198` streichen. | S |
| B5 | RD5 | Teamziehung, Fighter-Erzeugung, `Rounds = (Attacks+1)/2` und Achievement-Ansage stehen doppelt oder dreifach. „Probelauf?“ wird auf drei Arten entschieden (`lobby.IsSimulated`, `message.IsSimulated`, `_database.IsSandbox`). NPC-Teamgröße aus `PvpSettings`. | S |
| B6 | RD6 | `tools/BalancingSimulator.cs:117-125` kopiert die Defaults hart – gerade das Werkzeug für WP-09. Aus `BattleSettings.Default`/`BossSettings.Default` lesen. | S |
| B7 | RD7 | Tot oder nur für Tests: `BattleRandom.Seed`, `SiteLinks.None`, `OverlayServer.IsRunning` (wird mit A30 genutzt), `Format.Count` (ein Aufruf mit gleichem Singular/Plural), Fallback `"daten/"` in `Web/Program.cs:11`, `PackageVersion Microsoft.Extensions.Hosting` ohne Nutzer. | S |
| B8 | RD8 | Falsche Kommentare: `PollingPage.cs:6` (Overlay, 15 s), `site.css:1` („same palette“ – im Dunkelmodus nicht), `AppPaths.cs:4` („later backups“), Balancing-Kommentare in `PokeArenaOptions.cs:5`/`BattleSettings.cs:5` (siehe D3). | S |
| B9 | RD9 | Web: `BattleList` und `SpeciesName` liegen in `Layout/`, gehören nach `Shared/`. Der Hinweis „Daten konnten nicht geladen werden …“ steht viermal; CTS-/Dispose-Muster in Battle- und Trainer-Seite kopiert. | S |

## Teil C – Tests

TE hat die Lücken per Mutation nachgewiesen: Produktionscode gezielt kaputt gemacht, die ganze Suite blieb grün. Das geschah auf einer eigenen Kopie, der Snapshot blieb unverändert.

| # | IDs | Befund / Vorschlag |
|---|---|---|
| C1 | TE1 | **M** (schützt eine S-Falle) `TwitchAuthTests.cs:217` `Concurrent_refreshes_…` kann nicht fehlschlagen: Der Fake antwortet synchron. Ohne `_refreshLock` bleibt alles grün – genau der Fehler, der laut Core-CLAUDE.md den Bot abmeldet. Den Fake mit `TaskCompletionSource` sperren, bis ein zweiter Aufruf wartet. |
| C2 | TE2 | **M** Fällt in `TrainerRemoval.cs:45-46` `pending.TrainerId == target…` oder `pending.IsSimulated == …` weg, bleibt alles grün. Dann löscht `!entfernen @ash` gefolgt von `!entfernen @misty ja` Misty, ohne zu fragen. Zwei Fälle ergänzen. |
| C3 | TE3 | **M** Kein Test prüft das `Access` der echten Commands. `!entfernen`/`!bossreset` auf `Everyone` → alles grün, jeder Zuschauer kann löschen. Ein `[Theory]` über alle `IChatCommand` mit erwartetem Access, dazu ein Processor-Test mit dem echten `RemoveCommand` von einem Zuschauer. (nachgeprüft: kein Test referenziert die beiden Commands) |
| C4 | TE4 | **M** Outbox-Einträge bei PvP, NPC und Umbenennung ungetestet; nur Boss, Registrierung und Löschung prüfen die Queue. |
| C5 | TE5 | **M** `Republishing_queues_every_file` läuft ohne Kampf; die Kampfseiten aus `EnqueueEverythingAsync` zu streichen bleibt grün. |
| C6 | TE6 | **M** Die Reihenfolge „Live-Status erst nach den Abos“ (Core-CLAUDE.md) ist ungetestet. |
| C7 | TE7 | **M** `BossEncounterTests.cs:189` `DoesNotContain("AV", …)` kann nicht fehlschlagen. Doppelte Achievement-Ansagen bleiben unentdeckt, auch bei PvP und NPC. |
| C8 | TE8 | **M** `BossDraw` ist ungetestet: HP ohne `× N`, vertauschte Stufen und fehlender Cap bleiben grün. Der Boss-Test rechnet den Erwartungswert mit `BossDraw` selbst; der HP-Test nutzt noch 1,8. Damit hält kein Test die Änderung aus 1.0.1 (1,5) fest. |
| C9 | TE9 | **M** „Boss bereit“: Die Bedingungen offene Lobby, Probelauf und `!= until` lassen sich entfernen, ohne dass ein Test rot wird. |
| C10 | TE10 | **M** `ChatSender`: Split und Limiter im Sender ungetestet (nur die Einheiten). |
| C11 | TE11 | **M** `SiteGenerator`: nur die PvP-Seite geprüft. Fänger aus dem Log (Fix aus WP-08), NPC-Texte und Overlay-Top-5 ungetestet. |
| C12 | TE12 | **M** `EventSubSessionTests.cs:59-76`: echtes `Task.Delay(50)`. Der Test beweist weder die Gnadenfrist noch, dass ein Keepalive den Timer neu startet; auf einer langsamen Maschine kann er hängen (Verdacht). Mit Sperre statt Wartezeit neu schreiben. |
| C13 | TE13 | Starter nie Beute: Der Test erwischt den Fehler nur mit Seed 6 von 8. Deterministisch machen (TeamSize 2, Verlierer hat Starter + 1). |
| C14 | TE14, TE15 | Weitere Zweige ohne Treffer: Cache-Reset nach Löschung, Zurücksetzen von `failures`, gelöschter Lobby-Teilnehmer, `ArenaCommand`, Rundenlimit bei Gleichstand, `authorization_revoked`, 403 → `MissingPermission`, Achievement bleibt nach PvP-Verlust. |
| C15 | TE16 | `NpcServiceTests.The_dry_run_skips_the_cooldown` schickt simulierte Nachrichten an die Live-DB und prüft den echten Probelauf nicht. |
| C16 | TE17 | Die NPC-Tabelle der Anleitung ist hart codiert; der Test prüft nur Anzahl, ersten und letzten Eintrag. Gegen `data/npcs.json` vergleichen. |
| C17 | TE18 | Kein Architekturtest: „Web nur → Contracts“, „keine offiziellen Assets“ (Bilddateien und Sprite-Hosts in `wwwroot`/`*.razor`). |
| C18 | TE19 | Struktur: `RecordingSender` fünfmal, `FixedSeeds` viermal, `FakeTwitchConnection` zweimal kopiert → `Fakes/`. Testordner nicht gespiegelt (`Chat/ChatTests` testet EventSub, `TrainerListTests` enthält Shell/Settings/Backup). |
| C19 | TE20 | `Integration.Tests/TwitchLoginTests.cs:280` gilt laut Kommentar als „nur lesend“, löscht bei ungültigem Login aber den echten `PokeArena:twitch-token`. Kommentar oder Sichern/Wiederherstellen. |

**Überflüssig**

| # | IDs | Test(s) | Vorschlag | Fängt den Fehler danach |
|---|---|---|---|---|
| C20 | TE | `CommandTests.Status_command_answers_with_the_version_then_cools_down` (:41) | in den Alias-Test aufgehen lassen | `Version_runs_the_status_command_and_shares_its_cooldown` (:65) |
| C21 | TE | `ChatTests` Split-Fälle (:45-65, drei Facts) | ein `[Theory]` | dieselben Fälle als InlineData |
| C22 | TE | `SitePublishWorkerTests.Unexpected_errors_get_a_generic_reason` (:188) | Theory mit dem Grund-Teil von `A_failed_upload_backs_off_…` | die Theory; der Backoff-Teil bleibt eigener Test |

Nicht vorgeschlagen: `The_starter_is_never_the_loot` entfernen. Er hält eine Invariante fest und wird nur ersetzt (C13).

## Teil D – Doku und CLAUDE.md

| # | IDs | Befund / Vorschlag |
|---|---|---|
| D1 | RD10 | **M** Der Skill `/release` wurde nie committet: `.gitignore:25` `[Rr]elease/` trifft `.claude/skills/release/`. Commit a2fdf4f enthält nur `branch-code-review`, obwohl die Nachricht beide nennt. `CLAUDE.md` §1 verweist auf einen Skill, den kein Klon hat. `!.claude/skills/release/` eintragen und committen. (nachgeprüft) |
| D2 | RD12 | **M** Moderator-Frage widersprüchlich:<br>– `CLAUDE.md` §8: „`channel:bot` oder Moderator nötig“.<br>– ADR-0002-Addendum: mit User-Token nicht nötig, Moderator schützt nur vor Slow Mode; der Weg ohne Moderator ist ungetestet.<br>– spec §12: Moderator „for the higher rate limit“, das der Limiter bewusst nicht nutzt.<br>– README: Der Schritt fehlt in der Installationsanleitung für den Streamer.<br>Das betrifft die anstehende Installation. README um „Bot zum Moderator machen“ ergänzen, die drei Stellen angleichen. (nachgeprüft) |
| D3 | RD11, GL6 | **M** Fünf Stellen sagen „Balancing in der DB / Laufzeit-Editor geplant“: spec §10, `architecture.md:57-60`, ADR-0001, `PokeArenaOptions.cs:5`, `BattleSettings.cs:5`. Das widerspricht der Entscheidung vom 2026-11-04 (Code-Defaults, kein Editor, spec §13). ADR-0001 sagt außerdem, Herausforderungen lägen in der DB. Korrigieren, ADR-0001 mit Addendum. |
| D4 | RD14 | `spec.md` veraltet:<br>– §14 Chat-Wortlaut noch OPEN.<br>– §6 „bis die Website existiert“.<br>– §7 „drafts“.<br>– §9 Overlay als Website-Seite, 15-s-Polling, „CDN, once a custom domain is used“.<br>– §5 Beispiel-Logzeilen.<br>– §8 Schaltername weicht von der UI ab („Auch ohne Stream“). |
| D5 | RD15 | Veraltete Stände:<br>– `architecture.md:4-7` („WP-01…WP-06 built“), `:41`, `:140` (Platzhalter Shortcuts).<br>– `README.md:11` (1.0.0), README „Building“ ohne Web/Integration.Tests.<br>– `roadmap.md:30-31` (kaputte Aufzählung), `:284` (beantwortete UX-Frage), `:220` (Verbindungstest der Einstellungen nicht gebaut und nicht als Abweichung notiert – Verdacht).<br>– ADR-0002-Addendum „open setup question of WP-08“. |
| D6 | RD13 | `CHANGELOG.md:171`: verwaistes Fragment am Ende von [0.1.0]. |
| D7 | RD16 | `website-setup.md` §5 verspricht, dass der R2-Test läuft. Der Test liest den Bucket aber nur aus `appsettings*.json`, nicht aus `settings.json`. Schritt ergänzen oder den Test `settings.json` lesen lassen. |
| D8 | RD17 | `chat-texts.md:62,64`: Zeile 31 kennt nur „{n} Tokens“ (der Code hat 0/1/n); Zeile 33 ohne die Probelauf-Variante. |
| D9 | RD18 | Die Checkliste verbietet „real channel names“. Tests und Doku nutzen den eigenen Testkanal und `arena(-data).cloudfox.xyz`; jeder Reviewer meldet das neu. Regel präzisieren. → Frage 7. |
| D10 | RD19 | Größe: `CLAUDE.md` 9,9 KB (~2,5k Tokens), alle fünf zusammen ~21 KB (~5,3k).<br>Kürzen:<br>– Versionstabelle §3 (dritte Kopie).<br>– Core:8/53 (doppelt zu Root).<br>– Core:41-42 (tools-AOT steht in den tools-Dateien).<br>– Core:52 (fällt mit B2).<br>– Desktop:8-9/24-25 zusammenlegen, Desktop:26.<br>Ergänzen: `docs/chat-texts.md` und die Review-Dokumente in §1, Web in Zeile 18. |

## Fragen an den Nutzer

1. **Umfang und Reihenfolge.** Vorschlag:
   - **Vor der Installation beim Streamer** als Patch 1.0.4: D1, D2, A1, A2, A4, A10, A19, A29 und die Testlücken C1–C3. Das sind Ersteinrichtung, Verbindung, Herunterfahren und Mod-Rechte.
   - Danach ein Review-Paket 1.1.0 vor oder zusammen mit WP-09: der Rest von A, C und D, außerdem B. B1/B4/B6 berühren WP-09 direkt.
   - Oder alles in einem Paket?
2. **A3 Live-Status:** alle 5 min prüfen (einfach), oder Event-Vorrang plus Nachfragen nach dem Start (genauer)?
3. **A5 Drosselung:** Damit wird der Backlog-Punkt „Throttle repeated cooldown/error replies per user“ vorgezogen. Eine Fehlerantwort pro Zuschauer und 30 s – einverstanden?
4. **A6 Sicherung einspielen:** Bucket beim Republish auflisten und aufräumen (M) oder nur README ergänzen (Kampf-Ids bleiben dann ein Risiko)?
5. **A8 Overlay:** nur Hinweis in der Karte, oder lokale HTML-Datei, die per CORS pollt?
6. **A33 Formatversion:** N-1 lesen können oder Hinweis „Daten werden umgestellt“? Erst beim ersten Formatwechsel nötig – auch ins Backlog?
7. **D9:** Testkanal und eigene Domains in Tests/Doku erlauben (Checkliste präzisieren) oder neutral ersetzen?
8. Dieses Dokument und künftige Gesamt-Reviews: als Skill `complete-code-review` für PokeArena festhalten?

## Entscheidungen des Nutzers (2026-11-08)

Antwort: „Alles in einem Paket. halte den Ablauf als Skill fest.“

1. Alles in einem Paket: R1 auf `wp/r1-review-1.0.3`.
2.–7. Offen gelassen. Umgesetzt wird je der einfachere bzw. vorgeschlagene Weg (`CLAUDE.md` §7):
   - **A3:** Die stündliche Abfrage überschreibt keinen Status, den ein Event in den letzten 10 min gesetzt hat. Nach dem Start bei „offline“ wird 10 min lang alle 2 min nachgefragt.
   - **A5:** Eine Fehler- oder Hinweisantwort pro Zuschauer und 30 s. Der Backlog-Punkt entfällt damit.
   - **A6:** Beim Republish den Bucket auflisten und aufräumen, die Kampf-Sequenz über die höchste Id im Bucket heben.
   - **A8:** Nur der Hinweis in der Overlay-Karte. Die lokale HTML-Datei kommt ins Backlog.
   - **A33:** Hinweis auf der Website bei älterem Datenformat. N-1 lesen kommt ins Backlog, die Reihenfolge in den `/release`-Skill.
   - **D9:** Den eigenen Testkanal und die eigenen Domains erlauben; die Checkliste wird präzisiert.
8. Ablauf als Skill festhalten: `.claude/skills/complete-code-review/` (`SKILL.md`, `agents.md`).
