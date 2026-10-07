# wissen

ASP.NET-Core-MVC-Anwendung (.NET 10) mit Benutzerkonten über ASP.NET Core Identity und PostgreSQL als Datenbank. Die Oberfläche ist deutsch. Dazu gehört eine Konsolenanwendung für Sicherung und Wiederherstellung der Datenbank.

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL mit einer Rolle und einer Datenbank für die Anwendung, dazu die Client-Programme `pg_dump` und `pg_restore` für die Sicherung
- `dotnet-ef` für die Migrationen: `dotnet tool install --global dotnet-ef`
- optional [mdBook](https://rust-lang.github.io/mdBook/) für den Arbeitsbericht

## Einrichten

```bash
git clone https://github.com/thorstenkloehn/wissen.git
cd wissen
cp src/Wissen.Web/appsettings.example.json src/Wissen.Web/appsettings.json
```

In `src/Wissen.Web/appsettings.json` den Connection-String unter `ConnectionStrings:DefaultConnection` an die eigene Datenbank anpassen und das Passwort eintragen. Die Datei steht in `.gitignore` und wird nicht versioniert.

Anschließend die Datenbanktabellen anlegen:

```bash
dotnet run --project src/Wissen.Cli -- migrate
```

## Starten

```bash
dotnet run --project src/Wissen.Web
```

Die Anwendung läuft dann unter <http://localhost:5227> (Profil `https`: <https://localhost:7279>).

Neu registrierte Benutzer müssen ihr Konto bestätigen, bevor sie sich anmelden können. Ein E-Mail-Versand ist nicht eingerichtet; in der Entwicklungsumgebung wird der Bestätigungslink nach der Registrierung direkt angezeigt.

## Sicherung und Wiederherstellung

```bash
dotnet run --project src/Wissen.Cli -- backup                # nach backups/<Datenbank>-<Zeitstempel>.dump
dotnet run --project src/Wissen.Cli -- backup sicherung.dump # in eine bestimmte Datei
dotnet run --project src/Wissen.Cli -- restore sicherung.dump
```

`restore` überschreibt den aktuellen Stand der Datenbank und fragt deshalb vorher nach; mit `--ja` entfällt die Rückfrage. Die Konsolenanwendung nutzt den Connection-String aus `src/Wissen.Web/appsettings.json`.

## Befehle

| Befehl | Zweck |
| --- | --- |
| `dotnet build` | ganze Solution bauen |
| `dotnet test` | Tests ausführen |
| `dotnet run --project src/Wissen.Web` | Web-App starten |
| `dotnet run --project src/Wissen.Cli -- help` | Befehle der Konsolenanwendung anzeigen |
| `dotnet ef migrations add <Name> --project src/Wissen.Infrastructure --startup-project src/Wissen.Web` | neue Migration erzeugen |
| `dotnet run --project src/Wissen.Cli -- migrate` | Migrationen auf PostgreSQL anwenden |
| `mdbook serve Dokument --open` | Arbeitsbericht unter <http://localhost:3000> ansehen |

## Aufbau

| Pfad | Inhalt |
| --- | --- |
| `wissen.slnx` | Solution mit allen Projekten |
| `src/Wissen.Web/` | Web-App: `Program.cs`, `Controllers/`, `Views/`, `Areas/Identity/` (Anmeldung, Registrierung und Kontoverwaltung, ins Deutsche übersetzt), `wwwroot/` |
| `src/Wissen.Infrastructure/` | Datenzugriff: `ApplicationDbContext`, EF-Core-Migrationen für PostgreSQL, Datenbanksicherung |
| `src/Wissen.Cli/` | Konsolenanwendung; jeder Befehl ist ein Modul in `Modules/` |
| `tests/Wissen.Tests/` | xUnit-Tests |
| `Dokument/` | Arbeitsbericht als mdBook, siehe [Dokument/README.md](Dokument/README.md) |

## Lizenz

[MIT](LICENSE)
