# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Aufteilung des Repositorys

Die Solution `wissen.slnx` fasst vier Projekte zusammen (.NET 10):

- `src/Wissen.Web` ist die ASP.NET-Core-MVC-Anwendung (Controller, Views, Identity-Seiten, `Program.cs`).
- `src/Wissen.Infrastructure` ist die Klassenbibliothek für den Datenzugriff: `ApplicationDbContext` (`Data/`), die EF-Core-Migrationen (`Migrations/`), `DatabaseBackup` (`Backup/`) sowie `KontoAnlage`, `Rollen` und `GermanIdentityErrorDescriber` (`Identity/`). `AddInfrastructure()` in `DependencyInjection.cs` verdrahtet das für Web-App und Konsolenanwendung gemeinsam.
- `src/Wissen.Cli` ist die Konsolenanwendung mit den Befehlen `backup`, `backup-xml`, `restore`, `restore-xml`, `migrate`, `seiten-rendern`, `konto-anlegen`, `konto-admin`, `konto-passwort` und `konto-sperren`.
- `tests/Wissen.Tests` enthält die xUnit-Tests.
- `./Dokument` ist ein mdBook-Arbeitsbericht. **Der Inhalt stammt vom Nutzer:** Er schreibt Rohtexte in `Dokument/RAW`, Claude macht daraus fertige Artikel und korrigiert dabei nur Rechtschreibung und Grammatik, ohne etwas hinzuzudichten. Der genaue Ablauf steht in `Dokument/CLAUDE.md`; vor jeder Arbeit in `Dokument` dort nachlesen.
- `docs/` enthält die Konfiguration für docfx (`docfx.json`, `index.md`, `toc.yml`, `filterConfig.yml`). docfx ist ein lokales Werkzeug (`dotnet-tools.json`, nach einem frischen Klon `dotnet tool restore`) und erzeugt aus den XML-Kommentaren (`///`) der drei Projekte unter `src` die Dokumentation des Quelltextes: `docs/api/` (Zwischenstand) und `docs/_site/` (fertige Seiten) stehen in `.gitignore`. Die Migrationen sind über `filterConfig.yml` ausgenommen. Nicht mit `Dokument` verwechseln.

Abhängigkeiten zeigen nur in eine Richtung: `Wissen.Web` und `Wissen.Cli` verweisen auf `Wissen.Infrastructure`, nie umgekehrt. Im Wurzelordner darf kein Projekt liegen, weil es sonst die Dateien aller Unterordner (auch `Dokument`) mitkompilieren würde.

## Befehle

```bash
dotnet build                             # ganze Solution bauen
dotnet test                              # Tests ausführen
dotnet run --project src/Wissen.Web      # starten: http://localhost:5227 (Profil "https": https://localhost:7279)
dotnet run --project src/Wissen.Cli -- help            # Befehle der Konsolenanwendung
dotnet run --project src/Wissen.Cli -- backup [Datei]  # Sicherung, Standard: backups/<Datenbank>-<Zeitstempel>.dump
dotnet run --project src/Wissen.Cli -- backup-xml [Datei] # Seiten mit Versionsgeschichte als XML, Standard: backups/seiten-<Zeitstempel>.xml
dotnet run --project src/Wissen.Cli -- restore-xml <Datei> # XML-Sicherung einlesen, ersetzt Seiten mit gleichem Pfad (--ja: ohne Rückfrage)
dotnet run --project src/Wissen.Cli -- restore <Datei> # Wiederherstellung, überschreibt die Datenbank (--ja: ohne Rückfrage)
dotnet run --project src/Wissen.Cli -- seiten-rendern [--pruefen] # HTML aller Seiten und Versionen neu aus dem Markdown erzeugen (--pruefen: nur zählen)
dotnet run --project src/Wissen.Cli -- konto-anlegen <E-Mail> [--admin] # Konto anlegen, das sich sofort anmelden kann; fragt das Passwort ab
dotnet run --project src/Wissen.Cli -- konto-admin <E-Mail> [--entziehen] # Rolle Administrator geben oder wegnehmen
dotnet run --project src/Wissen.Cli -- konto-passwort <E-Mail> # neues Passwort setzen; fragt das Passwort ab
dotnet run --project src/Wissen.Cli -- konto-sperren <E-Mail> [--aufheben] # Konto sperren oder Sperre aufheben
dotnet ef migrations add <Name> --project src/Wissen.Infrastructure --startup-project src/Wissen.Web   # neue Migration
dotnet ef database update --project src/Wissen.Infrastructure --startup-project src/Wissen.Web         # Migrationen anwenden
mdbook serve Dokument --open             # Arbeitsbericht unter http://localhost:3000
dotnet docfx docs/docfx.json             # Dokumentation des Quelltextes nach docs/_site erzeugen (--serve: unter http://localhost:8080 ansehen)
```

Es gibt keine Lint-Konfiguration.

## Konsolenanwendung

Jeder Befehl ist ein Modul: eine Klasse in `src/Wissen.Cli/Modules`, die `ICommandModule` implementiert und in `src/Wissen.Cli/Program.cs` mit `AddScoped<ICommandModule, ...>()` angemeldet wird. Die Hilfe listet angemeldete Module automatisch auf. Module bekommen ihre Abhängigkeiten (z. B. `ApplicationDbContext`, `DatabaseBackup`) per Konstruktor.

`backup` und `restore` rufen `pg_dump` und `pg_restore` auf; die PostgreSQL-Client-Programme müssen im `PATH` liegen. Das Passwort wird über `PGPASSWORD` übergeben, nie als Argument. `restore` läuft in einer einzigen Transaktion (`--clean --if-exists --single-transaction`). `backup` und `backup-xml` legen ihre Datei über `PrivateFile.Create` an: nur für den eigenen Benutzer lesbar (Modus 600, neue Ordner 700), weil die Sicherungen Passwort-Hashes und die Versionsgeschichte enthalten.

`backup-xml` schreibt über `SeitenXmlExport` (`src/Wissen.Infrastructure/Backup/`) nur die Seiten: Id, Pfad, Kategorie, Markdown und alle Versionen (mit `SeitenVersion.Autor` als Attribut `autor`, wo bekannt), ohne das erzeugte HTML und ohne Konten. `restore-xml` liest die Datei über `SeitenXmlImport` wieder ein: Schlüssel ist der Pfad, eine vorhandene Seite behält ihre Id und bekommt Inhalt und Versionsgeschichte aus der Datei, Seiten, die nicht in der Datei stehen, bleiben unberührt; die Ids aus der Datei werden nicht übernommen. Das HTML entsteht dabei neu über `MarkdownRenderer` (`src/Wissen.Infrastructure/Rendering/`, Markdig plus HtmlSanitizer), den auch der `SeiteController` benutzt. Der Sanitizer lässt nur die Klassen durch, die Markdig selbst erzeugt (`MarkdigClasses`, dazu `language-*`), und als Stil nur `text-align`; frei gewählte Klassen und Stile (`{.klasse}`, `{style=...}`, `:::klasse`) werden entfernt, weil sich damit die ganze Seite überdecken ließe. Jede `id` im Inhalt bekommt den Vorsatz `MarkdownRenderer.IdPrefix` (`inhalt-`), Sprungziele innerhalb der Seite (`href="#..."`) ebenso, und `name` wird entfernt: Sonst ließen sich im Browser eingebaute Namen wie `getElementById` überdecken und die Skripte der Seite stören. Ein Bild von einem fremden Server (`MarkdownRenderer.IsExternal`; dazu zählt jede Adresse mit Schema, auch `https:/…` ohne doppelten Schrägstrich) wird zu einem Link „Bild: …“, damit beim Lesen einer Seite nichts von fremden Servern geladen wird; Bilder mit eigener Adresse (z. B. aus `wwwroot`) bleiben Bilder. Konten stehen nicht in der XML-Datei; vollständig wiederherstellen lässt sich nur aus einer `backup`-Sicherung.

`seiten-rendern` erzeugt über `SeitenRendern` (`src/Wissen.Infrastructure/Rendering/`) `Inhalt` aller Seiten und Versionen neu aus dem Markdown, ohne neue Versionen anzulegen. Das gespeicherte HTML wird roh ausgegeben und entsteht sonst nur beim Speichern: **Nach jeder Änderung am `MarkdownRenderer` (strengere Regel des Sanitizers) den Befehl ausführen**, lokal und auf dem Server, sonst gilt die Änderung nicht für schon gespeicherte Seiten. Was sich nicht umsetzen lässt, behält sein HTML und wird gemeldet (Exit-Code 1).

`konto-anlegen` legt über `KontoAnlage` ein Konto mit bestätigter E-Mail-Adresse an (Benutzername ist die E-Mail-Adresse). Das Passwort wird ohne Anzeige abgefragt oder aus einer Pipe gelesen, nie als Argument übergeben. `Program.cs` der Konsolenanwendung meldet dafür `AddIdentityCore` mit denselben Passwortregeln und deutschen Fehlermeldungen wie die Web-App an. Die Mindestlänge neuer Passwörter (`Passwortregeln.Mindestlaenge`, 12 Zeichen) setzt `AddInfrastructure()` für beide; schon vergebene kürzere Passwörter gelten weiter. `konto-anlegen --admin` und `konto-admin` vergeben die Rolle `Rollen.Administrator`; die Rolle wird beim ersten Vergeben angelegt, und eine Änderung gilt erst ab der nächsten Anmeldung des Kontos.

`konto-passwort` (`KontoAnlage.PasswortSetzenAsync`) ersetzt das Passwort, weil „Passwort vergessen“ ohne E-Mail-Versand nichts bewirkt; das neue Passwort wird vor dem Ersetzen geprüft. `konto-sperren` (`KontoAnlage.SperrenAsync`) setzt `LockoutEnd` auf unbegrenzt, `--aufheben` nimmt die Sperre zurück. Beide erneuern den Sicherheitsstempel: Bestehende Anmeldungen enden, sobald die Web-App ihn das nächste Mal prüft (spätestens nach 30 Minuten). Die Passwortabfrage aller Befehle liegt in `PasswortEingabe`.

`AddInfrastructure()` setzt `IdentityOptions.Stores.MaxLengthForKeys = 128`, denselben Wert wie die Identity-Oberfläche der Web-App. Ohne ihn hätte die Konsolenanwendung ein anderes Modell als die Migrationen, und `migrate` bräche auf einer leeren Datenbank mit „pending model changes“ ab.

Die Konsolenanwendung liest dieselbe `appsettings.json` wie die Web-App: `Wissen.Cli.csproj` kopiert `src/Wissen.Web/appsettings.json` beim Bauen in ihr Ausgabeverzeichnis. Nach einer Änderung der Datei also neu bauen.

## Veröffentlichen

`deploy/` enthält Vorlagen für einen Linux-Server (systemd-Dienst `wissen.service`, Umgebungsdatei `wissen.env.example`, `Caddyfile`, tägliche Sicherung über `wissen-backup.service` und `wissen-backup.timer`); die Schritte stehen in `README.md`. `appsettings.json` ist in beiden `.csproj` vom Veröffentlichen ausgenommen (`CopyToPublishDirectory="Never"`), weil sie das lokale Passwort enthält; auf dem Server kommen alle Einstellungen aus Umgebungsvariablen (`ConnectionStrings__DefaultConnection`, `AllowedHosts`, `RegistrierungErlaubt`).

## Datenbank

Die Anwendung nutzt PostgreSQL über `Npgsql.EntityFrameworkCore.PostgreSQL` (die SQLite-Voreinstellung der Vorlage wurde entfernt). Migrationen sind providerspezifisch für PostgreSQL erzeugt.

- `src/Wissen.Web/appsettings.json` enthält `ConnectionStrings:DefaultConnection` bewusst **mit Passwort** der lokalen Entwicklungsdatenbank (`Host=localhost;Port=5432;Database=thorsten;Username=thorsten;Password=...`). Das ist so gewollt; nicht entfernen. Die Datei steht deshalb in `.gitignore` und wird nicht versioniert; nach einem frischen Klon wird sie aus der Vorlage `appsettings.example.json` (ohne echtes Passwort) neu angelegt: `cp src/Wissen.Web/appsettings.example.json src/Wissen.Web/appsettings.json`, dann das Passwort eintragen.
- Die User-Secrets sind leer. Ein dort gesetzter Wert (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/Wissen.Web`) würde `appsettings.json` in der Umgebung `Development` überschreiben.
- Ohne passende PostgreSQL-Rolle scheitern `dotnet ef database update`, die Konsolenanwendung und alle Identity-Seiten mit `28P01` (Passwort-Authentifizierung fehlgeschlagen).

## Architektur

Ausgangspunkt ist die Vorlage `dotnet new mvc --auth Individual`; alles wird in `src/Wissen.Web/Program.cs` verdrahtet (kein `Startup`). Die Pfade in diesem Abschnitt sind relativ zu `src/Wissen.Web`. Die Namespaces heißen wie die Projekte (`Wissen.Web.*`, `Wissen.Infrastructure.*`, `Wissen.Cli.*`).

- **MVC und Razor Pages nebeneinander:** Eigene Seiten laufen über Controller/Views (`Controllers/`, `Views/`) mit Attribut-Routing: `app.MapControllers()` in `Program.cs`, jeder Controller braucht ein eigenes `[Route(...)]`; eine konventionelle Standardroute und einen `HomeController` gibt es nicht mehr. `docController` hat die Auffang-Route `[Route("{*path}")]`: Seine Aktion `HandleAll` bekommt jede sonst nicht belegte Adresse als `path` (nur GET und HEAD, sonst 405) und zeigt `Views/doc/Index.cshtml`. Fehlt die Seite, zeigt die Ansicht das mit Status 404 an und bietet Angemeldeten an, sie anzulegen; bei einem ungültigen Pfad (`Seite.IsValidPath`, z. B. `/wp-login.php`) wird die Datenbank gar nicht erst gefragt. `/` leitet auf `/doc` um; die Fehlerseite liegt unter `/Error` (`ErrorController`). `Views/Home/` wird derzeit von keinem Controller angezeigt. Die Login-/Registrierungsseiten kommen als Razor Pages aus dem Paket `Microsoft.AspNetCore.Identity.UI` (`AddDefaultIdentity` + `MapRazorPages`) , sind aber alle per Scaffolding nach `Areas/Identity/Pages/Account` (inklusive `Manage`) ins Projekt geholt und ins Deutsche übersetzt; diese Dateien überschreiben die Seiten aus dem Paket. Erneutes Scaffolding (`dotnet aspnet-codegenerator identity`, benötigt vorübergehend das Paket `Microsoft.VisualStudio.Web.CodeGeneration.Design`) würde mit `--force` die Übersetzungen überschreiben. Die gesamte Oberfläche ist deutsch (Sie-Form); neue Texte ebenfalls auf Deutsch schreiben. Die Identity-Fehlermeldungen sind über `GermanIdentityErrorDescriber` (`src/Wissen.Infrastructure/Identity/`) deutsch.
- **Identity:** `IdentityUser` ohne eigene Benutzerklasse; `ApplicationDbContext` (`src/Wissen.Infrastructure/Data/`) erbt von `IdentityDbContext` und ist der einzige DbContext. Eigene Entitäten kommen als `DbSet` dort hinzu.
- **Registrierung abgeschaltet:** Ohne `"RegistrierungErlaubt": true` in `appsettings.json` antworten `/Identity/Account/Register` und `/Identity/Account/RegisterConfirmation` mit 404 (`RegistrierungGesperrtFilter`, in `Program.cs` angemeldet) und die Links dorthin sind ausgeblendet. Konten legt die Konsolenanwendung an (`konto-anlegen`); die Registrierung muss dafür nie geöffnet werden.
- **Bestätigte Konten erforderlich:** `SignIn.RequireConfirmedAccount = true`, es ist aber kein echter E-Mail-Versand eingerichtet. `RegisterConfirmation` zeigt den Bestätigungslink bewusst nicht mehr an (damit konnte jeder Besucher sein Konto selbst bestätigen); ein im Browser registriertes Konto kann sich deshalb nicht anmelden, solange kein E-Mail-Versand eingerichtet ist.
- **Länge des Inhalts:** Das Markdown einer Seite ist in beiden Formularen auf `Seite.MaxMarkdownLength` (200 000 Zeichen) begrenzt, das daraus erzeugte HTML auf `Seite.MaxHtmlLength` (600 000 Zeichen): `MarkdownRenderer.ToHtml(markdown, maxHtmlLength)` wirft sonst `MarkdownException`, ebenso bei zu tiefer Verschachtelung; der `SeiteController` zeigt die Meldung im Formular. `restore-xml` prüft beide Längen nicht.
- **Änderungen je Konto:** `SpeicherBegrenzung` (in `Program.cs` mit `AnmeldeBegrenzung` zu einem `GlobalLimiter` verkettet) lässt je Konto nur eine POST-Anfrage an `/seite/...` zur selben Zeit und 20 in 5 Minuten zu, danach antwortet die App mit 429. Grund: Markdown umzusetzen kann bei ungünstigen Eingaben viele Sekunden dauern, und jede Änderung speichert eine Version. Dafür steht `app.UseAuthentication()` vor `app.UseRateLimiter()`.
- **Gleichzeitiges Speichern:** Speichern zwei Konten dieselbe Seite im selben Moment (oder legen denselben Pfad an), lässt der eindeutige Index nur eines durch; der `SeiteController` fängt das ab (`IstSchonVergeben`, PostgreSQL-Fehler `23505`) und zeigt dem anderen eine Meldung statt Fehler 500.
- **Autor:** Jede Version speichert in `SeitenVersion.Autor` den Benutzernamen (E-Mail-Adresse) des Kontos, das sie angelegt hat, als Text und nicht als Verweis, damit die Angabe ein gelöschtes Konto überdauert; ältere Versionen haben keinen Autor („unbekannt“). Die Versionsgeschichte zeigt ihn nur Angemeldeten.
- **Rollen:** Jedes angemeldete Konto darf Seiten anlegen, bearbeiten und zurücksetzen. Löschen (samt Versionsgeschichte, nicht umkehrbar) verlangt die Rolle `Rollen.Administrator` (`[Authorize(Roles = ...)]` an beiden `Loeschen`-Aktionen); der Link ist für andere ausgeblendet. Die Rolle vergibt nur die Konsolenanwendung, in der Web-App gibt es dafür keine Oberfläche.
- **Anmeldeversuche:** `AnmeldeBegrenzung` (`Areas/Identity/`, in `Program.cs` über `AddRateLimiter`/`UseRateLimiter` angemeldet) lässt je IP-Adresse (bei IPv6 je /64-Netz) 5 Anmeldeversuche in 5 Minuten zu, danach antwortet die App mit 429. Gezählt werden POST-Anfragen an `Login`, `LoginWith2fa` und `LoginWithRecoveryCode`. Die Kontosperre (`lockoutOnFailure: true`) greift erst nach 20 Fehlversuchen für 5 Minuten, damit nicht jeder, der die E-Mail-Adresse kennt, ein Konto mit wenigen Versuchen sperren kann. Hinter dem Reverse-Proxy braucht das `ASPNETCORE_FORWARDEDHEADERS_ENABLED='true'`, sonst kämen alle Anfragen von 127.0.0.1 und teilten sich eine Grenze.
- **Sicherheits-Header:** Eine Middleware am Anfang von `Program.cs` setzt für jede Antwort `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options` und `Referrer-Policy`. Bilder sind nur vom eigenen Server erlaubt (`img-src 'self' data:`), auch in Views keine Bilder von fremden Servern einbinden. Skripte sind nur aus eigenen Dateien erlaubt: keine Inline-Skripte, keine `onclick`-Attribute und keine Skripte von fremden Servern in Views verwenden, sonst blockiert sie der Browser.
- **Statische Dateien:** `MapStaticAssets()` / `.WithStaticAssets()` (Build-Zeit-Fingerprinting) statt `UseStaticFiles`; Client-Bibliotheken liegen unter `wwwroot/lib`.
