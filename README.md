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

Die Registrierung im Browser ist abgeschaltet. Ein Konto legt die Konsolenanwendung an; das Passwort wird dabei abgefragt und nicht angezeigt:

```bash
dotnet run --project src/Wissen.Cli -- konto-anlegen name@example.org
```

Das Konto kann sich sofort anmelden. Ein E-Mail-Versand ist nicht eingerichtet; ein im Browser registriertes Konto (`"RegistrierungErlaubt": true`) ließe sich deshalb nicht bestätigen.

## Veröffentlichen auf einem Server

Die Vorlagen in `deploy/` gehen von einem eigenen Linux-Server mit systemd aus: Die App läuft als Dienst und lauscht nur lokal, davor steht [Caddy](https://caddyserver.com/) als Reverse-Proxy und besorgt das HTTPS-Zertifikat. Auf dem Server werden die [ASP.NET Core Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0), PostgreSQL und Caddy gebraucht.

`appsettings.json` wird nicht mit veröffentlicht, weil sie das Passwort der lokalen Datenbank enthält. Auf dem Server kommen alle Einstellungen aus `/etc/wissen/wissen.env`.

1. Lokal veröffentlichen und die beiden Ordner nach `/opt/wissen/web` und `/opt/wissen/cli` auf den Server kopieren:

   ```bash
   dotnet publish src/Wissen.Web -c Release -o publish/web
   dotnet publish src/Wissen.Cli -c Release -o publish/cli
   ```

2. Auf dem Server einen Benutzer für den Dienst sowie eine eigene Datenbankrolle und Datenbank anlegen (eigenes Passwort, nicht das lokale):

   ```bash
   sudo useradd --system --no-create-home wissen
   sudo -u postgres createuser --pwprompt wissen
   sudo -u postgres createdb --owner wissen wissen
   ```

3. `deploy/wissen.env.example` nach `/etc/wissen/wissen.env` kopieren, Domain und Passwort eintragen und die Datei mit `chmod 600` schützen.

4. Die Datenbanktabellen anlegen:

   ```bash
   sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll migrate'
   ```

5. `deploy/wissen.service` nach `/etc/systemd/system/` kopieren und den Dienst starten:

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now wissen
   ```

6. `deploy/Caddyfile` nach `/etc/caddy/Caddyfile` kopieren, die Domain eintragen und `sudo systemctl reload caddy` ausführen.

7. Das erste Konto anlegen; das Passwort wird abgefragt:

   ```bash
   sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll konto-anlegen name@example.org'
   ```

### Automatische Sicherung

`deploy/wissen-backup.service` und `deploy/wissen-backup.timer` nach `/etc/systemd/system/` kopieren und den Timer einschalten:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now wissen-backup.timer
sudo systemctl start wissen-backup    # einmal sofort sichern
systemctl status wissen-backup        # Ergebnis ansehen
systemctl list-timers wissen-backup   # nächster Lauf
```

Die Sicherung läuft jede Nacht gegen 3 Uhr und legt eine Datei `wissen-<Zeitstempel>.dump` in `/var/lib/wissen/backups` ab; Sicherungen, die älter als 30 Tage sind, werden gelöscht. Die Dateien enthalten alle Artikel, Versionen und Konten.

Diese Sicherungen liegen auf demselben Server wie die Datenbank und schützen nicht vor dessen Verlust. Deshalb regelmäßig auf einen anderen Rechner holen, zum Beispiel:

```bash
rsync -a --rsync-path="sudo rsync" BENUTZER@IHRE-DOMAIN.de:/var/lib/wissen/backups/ ~/wissen-backups/
```

Wiederherstellen (überschreibt die Datenbank):

```bash
sudo systemctl stop wissen
sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll restore /var/lib/wissen/backups/DATEI.dump'
sudo systemctl start wissen
```

## Sicherung und Wiederherstellung

Es gibt zwei Arten von Sicherungen:

| | `backup` / `restore` | `backup-xml` / `restore-xml` |
| --- | --- | --- |
| Inhalt | die ganze Datenbank: Seiten, Versionen und Konten | nur die Seiten mit ihrer Versionsgeschichte |
| Format | `.dump` von `pg_dump`, nicht lesbar | `.xml`, mit jedem Texteditor lesbar |
| Beim Einlesen | die Datenbank wird vollständig überschrieben | nur Seiten mit gleichem Pfad werden ersetzt |
| Gedacht für | vollständige Wiederherstellung nach einem Verlust | Seiten ansehen, aufbewahren oder in eine andere Installation übernehmen |

### Ganze Datenbank

```bash
dotnet run --project src/Wissen.Cli -- backup                # nach backups/<Datenbank>-<Zeitstempel>.dump
dotnet run --project src/Wissen.Cli -- backup sicherung.dump # in eine bestimmte Datei
dotnet run --project src/Wissen.Cli -- restore sicherung.dump
```

`restore` überschreibt den aktuellen Stand der Datenbank und fragt deshalb vorher nach; mit `--ja` entfällt die Rückfrage. Die Konsolenanwendung nutzt den Connection-String aus `src/Wissen.Web/appsettings.json`.

### Seiten als XML

Sichern:

```bash
dotnet run --project src/Wissen.Cli -- backup-xml            # nach backups/seiten-<Zeitstempel>.xml
dotnet run --project src/Wissen.Cli -- backup-xml seiten.xml # in eine bestimmte Datei
```

Die Datei enthält je Seite Id, Pfad, Kategorie, Markdown und alle Versionen mit Nummer und Zeitpunkt (in UTC):

```xml
<?xml version="1.0" encoding="utf-8"?>
<seiten erstelltAm="2026-10-08T15:51:37.3426179Z">
  <seite id="1">
    <path>doc</path>
    <kategorie>Hauptseite</kategorie>
    <markdown>## start

Willkommen auf meine Seite</markdown>
    <versionsgeschichte>
      <version nummer="1" erstelltAm="2026-10-08T15:01:19.957186Z">
        <kategorie>Hauptseite</kategorie>
        <markdown>## start</markdown>
      </version>
      <version nummer="2" erstelltAm="2026-10-08T15:11:27.135033Z">
        <kategorie>Hauptseite</kategorie>
        <markdown>## start

Willkommen auf meine Seite</markdown>
      </version>
    </versionsgeschichte>
  </seite>
</seiten>
```

Nicht enthalten sind die Konten und das erzeugte HTML; das HTML entsteht beim Einlesen neu aus dem Markdown.

Einlesen:

```bash
dotnet run --project src/Wissen.Cli -- restore-xml seiten.xml       # mit Rückfrage, falls Seiten ersetzt werden
dotnet run --project src/Wissen.Cli -- restore-xml seiten.xml --ja  # ohne Rückfrage
```

Dabei gilt:

- Der Pfad entscheidet. Gibt es zu einem Pfad schon eine Seite, wird sie samt Versionsgeschichte durch den Stand aus der Datei ersetzt.
- Seiten, die nicht in der Datei stehen, bleiben unverändert.
- Die Ids aus der Datei werden nicht übernommen: Eine vorhandene Seite behält ihre Id, neue Seiten bekommen eine neue.
- Ist die Datei fehlerhaft (ungültiger Pfad, fehlendes Element, abgeschnitten), wird nichts geändert und die Meldung nennt die Stelle.

Was der Befehl in welchem Fall meldet:

| Schritt | Ergebnis |
| --- | --- |
| Einlesen in eine leere Datenbank | `1 neu, 0 ersetzt` |
| Erneut einlesen, Rückfrage mit „nein“ beantwortet | `Abgebrochen.`, nichts geändert |
| Erneut einlesen mit `--ja` | `0 neu, 1 ersetzt` |
| Danach wieder mit `backup-xml` sichern | Datei stimmt mit der Ausgangsdatei überein |

## Befehle

| Befehl | Zweck |
| --- | --- |
| `dotnet build` | ganze Solution bauen |
| `dotnet test` | Tests ausführen |
| `dotnet run --project src/Wissen.Web` | Web-App starten |
| `dotnet run --project src/Wissen.Cli -- help` | Befehle der Konsolenanwendung anzeigen |
| `dotnet ef migrations add <Name> --project src/Wissen.Infrastructure --startup-project src/Wissen.Web` | neue Migration erzeugen |
| `dotnet run --project src/Wissen.Cli -- migrate` | Migrationen auf PostgreSQL anwenden |
| `dotnet run --project src/Wissen.Cli -- konto-anlegen <E-Mail>` | Konto anlegen, das sich sofort anmelden kann |
| `mdbook serve Dokument --open` | Arbeitsbericht unter <http://localhost:3000> ansehen |

## Aufbau

| Pfad | Inhalt |
| --- | --- |
| `wissen.slnx` | Solution mit allen Projekten |
| `src/Wissen.Web/` | Web-App: `Program.cs`, `Controllers/`, `Views/`, `Areas/Identity/` (Anmeldung, Registrierung und Kontoverwaltung, ins Deutsche übersetzt), `wwwroot/` |
| `src/Wissen.Infrastructure/` | Datenzugriff: `ApplicationDbContext`, EF-Core-Migrationen für PostgreSQL, Datenbanksicherung |
| `src/Wissen.Cli/` | Konsolenanwendung; jeder Befehl ist ein Modul in `Modules/` |
| `tests/Wissen.Tests/` | xUnit-Tests |
| `deploy/` | Vorlagen für den Server: systemd-Dienst, Umgebungsdatei, Caddyfile, tägliche Sicherung |
| `Dokument/` | Arbeitsbericht als mdBook, siehe [Dokument/README.md](Dokument/README.md) |

## Lizenz

[MIT](LICENSE)
