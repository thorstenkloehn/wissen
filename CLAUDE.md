# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Aufteilung des Repositorys

Die Solution `wissen.slnx` fasst vier Projekte zusammen (.NET 10):

- `src/Wissen.Web` ist die ASP.NET-Core-MVC-Anwendung (Controller, Views, Identity-Seiten, `Program.cs`).
- `src/Wissen.Infrastructure` ist die Klassenbibliothek für den Datenzugriff: `ApplicationDbContext` (`Data/`), die EF-Core-Migrationen (`Migrations/`) und `DatabaseBackup` (`Backup/`). `AddInfrastructure()` in `DependencyInjection.cs` verdrahtet das für Web-App und Konsolenanwendung gemeinsam.
- `src/Wissen.Cli` ist die Konsolenanwendung mit den Befehlen `backup`, `restore` und `migrate`.
- `tests/Wissen.Tests` enthält die xUnit-Tests.
- `./Dokument` ist ein mdBook-Arbeitsbericht. **Der Inhalt stammt vom Nutzer:** Er schreibt Rohtexte in `Dokument/RAW`, Claude macht daraus fertige Artikel und korrigiert dabei nur Rechtschreibung und Grammatik, ohne etwas hinzuzudichten. Der genaue Ablauf steht in `Dokument/CLAUDE.md`; vor jeder Arbeit in `Dokument` dort nachlesen.

Abhängigkeiten zeigen nur in eine Richtung: `Wissen.Web` und `Wissen.Cli` verweisen auf `Wissen.Infrastructure`, nie umgekehrt. Im Wurzelordner darf kein Projekt liegen, weil es sonst die Dateien aller Unterordner (auch `Dokument`) mitkompilieren würde.

## Befehle

```bash
dotnet build                             # ganze Solution bauen
dotnet test                              # Tests ausführen
dotnet run --project src/Wissen.Web      # starten: http://localhost:5227 (Profil "https": https://localhost:7279)
dotnet run --project src/Wissen.Cli -- help            # Befehle der Konsolenanwendung
dotnet run --project src/Wissen.Cli -- backup [Datei]  # Sicherung, Standard: backups/<Datenbank>-<Zeitstempel>.dump
dotnet run --project src/Wissen.Cli -- restore <Datei> # Wiederherstellung, überschreibt die Datenbank (--ja: ohne Rückfrage)
dotnet ef migrations add <Name> --project src/Wissen.Infrastructure --startup-project src/Wissen.Web   # neue Migration
dotnet ef database update --project src/Wissen.Infrastructure --startup-project src/Wissen.Web         # Migrationen anwenden
mdbook serve Dokument --open             # Arbeitsbericht unter http://localhost:3000
```

Es gibt keine Lint-Konfiguration.

## Konsolenanwendung

Jeder Befehl ist ein Modul: eine Klasse in `src/Wissen.Cli/Modules`, die `ICommandModule` implementiert und in `src/Wissen.Cli/Program.cs` mit `AddScoped<ICommandModule, ...>()` angemeldet wird. Die Hilfe listet angemeldete Module automatisch auf. Module bekommen ihre Abhängigkeiten (z. B. `ApplicationDbContext`, `DatabaseBackup`) per Konstruktor.

`backup` und `restore` rufen `pg_dump` und `pg_restore` auf; die PostgreSQL-Client-Programme müssen im `PATH` liegen. Das Passwort wird über `PGPASSWORD` übergeben, nie als Argument. `restore` läuft in einer einzigen Transaktion (`--clean --if-exists --single-transaction`).

Die Konsolenanwendung liest dieselbe `appsettings.json` wie die Web-App: `Wissen.Cli.csproj` kopiert `src/Wissen.Web/appsettings.json` beim Bauen in ihr Ausgabeverzeichnis. Nach einer Änderung der Datei also neu bauen.

## Veröffentlichen

`deploy/` enthält Vorlagen für einen Linux-Server (systemd-Dienst `wissen.service`, Umgebungsdatei `wissen.env.example`, `Caddyfile`); die Schritte stehen in `README.md`. `appsettings.json` ist in beiden `.csproj` vom Veröffentlichen ausgenommen (`CopyToPublishDirectory="Never"`), weil sie das lokale Passwort enthält; auf dem Server kommen alle Einstellungen aus Umgebungsvariablen (`ConnectionStrings__DefaultConnection`, `AllowedHosts`, `RegistrierungErlaubt`).

## Datenbank

Die Anwendung nutzt PostgreSQL über `Npgsql.EntityFrameworkCore.PostgreSQL` (die SQLite-Voreinstellung der Vorlage wurde entfernt). Migrationen sind providerspezifisch für PostgreSQL erzeugt.

- `src/Wissen.Web/appsettings.json` enthält `ConnectionStrings:DefaultConnection` bewusst **mit Passwort** der lokalen Entwicklungsdatenbank (`Host=localhost;Port=5432;Database=thorsten;Username=thorsten;Password=...`). Das ist so gewollt; nicht entfernen. Die Datei steht deshalb in `.gitignore` und wird nicht versioniert; nach einem frischen Klon wird sie aus der Vorlage `appsettings.example.json` (ohne echtes Passwort) neu angelegt: `cp src/Wissen.Web/appsettings.example.json src/Wissen.Web/appsettings.json`, dann das Passwort eintragen.
- Die User-Secrets sind leer. Ein dort gesetzter Wert (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/Wissen.Web`) würde `appsettings.json` in der Umgebung `Development` überschreiben.
- Ohne passende PostgreSQL-Rolle scheitern `dotnet ef database update`, die Konsolenanwendung und alle Identity-Seiten mit `28P01` (Passwort-Authentifizierung fehlgeschlagen).

## Architektur

Ausgangspunkt ist die Vorlage `dotnet new mvc --auth Individual`; alles wird in `src/Wissen.Web/Program.cs` verdrahtet (kein `Startup`). Die Pfade in diesem Abschnitt sind relativ zu `src/Wissen.Web`. Die Namespaces heißen wie die Projekte (`Wissen.Web.*`, `Wissen.Infrastructure.*`, `Wissen.Cli.*`).

- **MVC und Razor Pages nebeneinander:** Eigene Seiten laufen über Controller/Views (`Controllers/`, `Views/`) mit Attribut-Routing: `app.MapControllers()` in `Program.cs`, jeder Controller braucht ein eigenes `[Route(...)]`; eine konventionelle Standardroute und einen `HomeController` gibt es nicht mehr. `docController` hat die Auffang-Route `[Route("{*path}")]`: Seine Aktion `HandleAll` bekommt jede sonst nicht belegte Adresse als `path` und zeigt `Views/doc/Index.cshtml`, es gibt also kein 404 mehr. `/` leitet auf `/doc` um; die Fehlerseite liegt unter `/Error` (`ErrorController`). `Views/Home/` wird derzeit von keinem Controller angezeigt. Die Login-/Registrierungsseiten kommen als Razor Pages aus dem Paket `Microsoft.AspNetCore.Identity.UI` (`AddDefaultIdentity` + `MapRazorPages`) , sind aber alle per Scaffolding nach `Areas/Identity/Pages/Account` (inklusive `Manage`) ins Projekt geholt und ins Deutsche übersetzt; diese Dateien überschreiben die Seiten aus dem Paket. Erneutes Scaffolding (`dotnet aspnet-codegenerator identity`, benötigt vorübergehend das Paket `Microsoft.VisualStudio.Web.CodeGeneration.Design`) würde mit `--force` die Übersetzungen überschreiben. Die gesamte Oberfläche ist deutsch (Sie-Form); neue Texte ebenfalls auf Deutsch schreiben. Die Identity-Fehlermeldungen sind über `GermanIdentityErrorDescriber` (`Areas/Identity/`) deutsch.
- **Identity:** `IdentityUser` ohne eigene Benutzerklasse; `ApplicationDbContext` (`src/Wissen.Infrastructure/Data/`) erbt von `IdentityDbContext` und ist der einzige DbContext. Eigene Entitäten kommen als `DbSet` dort hinzu.
- **Registrierung abgeschaltet:** Ohne `"RegistrierungErlaubt": true` in `appsettings.json` antworten `/Identity/Account/Register` und `/Identity/Account/RegisterConfirmation` mit 404 (`RegistrierungGesperrtFilter`, in `Program.cs` angemeldet) und die Links dorthin sind ausgeblendet. Um ein Konto anzulegen, den Wert vorübergehend auf `true` setzen und neu starten.
- **Bestätigte Konten erforderlich:** `SignIn.RequireConfirmedAccount = true`, es ist aber kein echter E-Mail-Versand eingerichtet. Neu registrierte Benutzer können sich also erst anmelden, nachdem die Bestätigung über den nach der Registrierung angezeigten Link erfolgt ist. Deshalb darf die Registrierung auf einem öffentlichen Server nicht offen sein.
- **Kontosperre:** Die Login-Seite ruft `PasswordSignInAsync` mit `lockoutOnFailure: true` auf (Standard von Identity: 5 Fehlversuche, 5 Minuten Sperre).
- **Statische Dateien:** `MapStaticAssets()` / `.WithStaticAssets()` (Build-Zeit-Fingerprinting) statt `UseStaticFiles`; Client-Bibliotheken liegen unter `wwwroot/lib`.
