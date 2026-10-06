# wissen

ASP.NET-Core-MVC-Anwendung (.NET 10) mit Benutzerkonten über ASP.NET Core Identity und PostgreSQL als Datenbank. Die Oberfläche ist deutsch.

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL mit einer Rolle und einer Datenbank für die Anwendung
- `dotnet-ef` für die Migrationen: `dotnet tool install --global dotnet-ef`
- optional [mdBook](https://rust-lang.github.io/mdBook/) für den Arbeitsbericht

## Einrichten

```bash
git clone https://github.com/thorstenkloehn/wissen.git
cd wissen
cp appsettings.example.json appsettings.json
```

In `appsettings.json` den Connection-String unter `ConnectionStrings:DefaultConnection` an die eigene Datenbank anpassen und das Passwort eintragen. Die Datei steht in `.gitignore` und wird nicht versioniert.

Anschließend die Datenbanktabellen anlegen:

```bash
dotnet ef database update
```

## Starten

```bash
dotnet run
```

Die Anwendung läuft dann unter <http://localhost:5227> (Profil `https`: <https://localhost:7279>).

Neu registrierte Benutzer müssen ihr Konto bestätigen, bevor sie sich anmelden können. Ein E-Mail-Versand ist nicht eingerichtet; in der Entwicklungsumgebung wird der Bestätigungslink nach der Registrierung direkt angezeigt.

## Befehle

| Befehl | Zweck |
| --- | --- |
| `dotnet build` | bauen |
| `dotnet run` | starten |
| `dotnet ef migrations add <Name> -o Migrations` | neue Migration erzeugen |
| `dotnet ef database update` | Migrationen auf PostgreSQL anwenden |
| `mdbook serve Dokument --open` | Arbeitsbericht unter <http://localhost:3000> ansehen |

## Aufbau

| Pfad | Inhalt |
| --- | --- |
| `Program.cs` | Verdrahtung der Anwendung (kein `Startup`) |
| `Controllers/`, `Views/` | eigene Seiten (MVC) |
| `Areas/Identity/` | Anmeldung, Registrierung und Kontoverwaltung als Razor Pages, ins Deutsche übersetzt |
| `Data/` | `ApplicationDbContext` |
| `Migrations/` | EF-Core-Migrationen für PostgreSQL |
| `wwwroot/` | statische Dateien und Client-Bibliotheken |
| `Dokument/` | Arbeitsbericht als mdBook, siehe [Dokument/README.md](Dokument/README.md) |

## Lizenz

[MIT](LICENSE)
