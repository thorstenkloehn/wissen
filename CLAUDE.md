# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Aufteilung des Repositorys

- `./` ist die ASP.NET-Core-MVC-Anwendung `wissen` (.NET 10, `wissen.csproj`).
- `./Dokument` ist ein mdBook-Arbeitsbericht. **Der Inhalt stammt vom Nutzer:** Er schreibt Rohtexte in `Dokument/RAW`, Claude macht daraus fertige Artikel und korrigiert dabei nur Rechtschreibung und Grammatik, ohne etwas hinzuzudichten. Der genaue Ablauf steht in `Dokument/CLAUDE.md`; vor jeder Arbeit in `Dokument` dort nachlesen.

`Dokument/**` ist in `wissen.csproj` über `DefaultItemExcludes` vom .NET-Build ausgeschlossen, damit weder die Markdown-Quellen noch die HTML-Ausgabe (`Dokument/book`) in die Web-App geraten. Diesen Ausschluss beibehalten.

## Befehle

```bash
dotnet build                      # bauen
dotnet run                        # starten: http://localhost:5227 (Profil "https": https://localhost:7279)
dotnet ef migrations add <Name> -o Migrations   # neue Migration
dotnet ef database update         # Migrationen auf PostgreSQL anwenden
mdbook serve Dokument --open      # Arbeitsbericht unter http://localhost:3000
```

Es gibt bisher kein Testprojekt und keine Lint-Konfiguration.

## Datenbank

Die Anwendung nutzt PostgreSQL über `Npgsql.EntityFrameworkCore.PostgreSQL` (die SQLite-Voreinstellung der Vorlage wurde entfernt). Migrationen sind providerspezifisch für PostgreSQL erzeugt.

- `appsettings.json` enthält `ConnectionStrings:DefaultConnection` bewusst **ohne Passwort** (`Host=localhost;Port=5432;Database=thorsten;Username=thorsten`).
- Der vollständige Connection-String mit Passwort gehört in die User-Secrets (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`); sie überschreiben `appsettings.json` in der Umgebung `Development`.
- Ohne dieses Secret und eine passende PostgreSQL-Rolle scheitern `dotnet ef database update` und alle Identity-Seiten mit `28P01` (Passwort-Authentifizierung fehlgeschlagen).

## Architektur

Ausgangspunkt ist die Vorlage `dotnet new mvc --auth Individual`; alles wird in `Program.cs` verdrahtet (kein `Startup`).

- **MVC und Razor Pages nebeneinander:** Eigene Seiten laufen über Controller/Views (`Controllers/`, `Views/`, Route `{controller=Home}/{action=Index}/{id?}`). Die Login-/Registrierungsseiten kommen als Razor Pages aus dem Paket `Microsoft.AspNetCore.Identity.UI` (`AddDefaultIdentity` + `MapRazorPages`) , sind aber alle per Scaffolding nach `Areas/Identity/Pages/Account` (inklusive `Manage`) ins Projekt geholt und ins Deutsche übersetzt; diese Dateien überschreiben die Seiten aus dem Paket. Erneutes Scaffolding (`dotnet aspnet-codegenerator identity`, benötigt vorübergehend das Paket `Microsoft.VisualStudio.Web.CodeGeneration.Design`) würde mit `--force` die Übersetzungen überschreiben. Die gesamte Oberfläche ist deutsch (Sie-Form); neue Texte ebenfalls auf Deutsch schreiben. Die Identity-Fehlermeldungen sind über `GermanIdentityErrorDescriber` (`Areas/Identity/`) deutsch.
- **Identity:** `IdentityUser` ohne eigene Benutzerklasse; `ApplicationDbContext` (`Data/`) erbt von `IdentityDbContext` und ist der einzige DbContext. Eigene Entitäten kommen als `DbSet` dort hinzu.
- **Bestätigte Konten erforderlich:** `SignIn.RequireConfirmedAccount = true`, es ist aber kein `IEmailSender` registriert. Neu registrierte Benutzer können sich also erst anmelden, nachdem die Bestätigung über den in der Entwicklungsumgebung angezeigten Link erfolgt ist.
- **Statische Dateien:** `MapStaticAssets()` / `.WithStaticAssets()` (Build-Zeit-Fingerprinting) statt `UseStaticFiles`; Client-Bibliotheken liegen unter `wwwroot/lib`.
