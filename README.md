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

Die Registrierung ist abgeschaltet. Um ein Konto anzulegen, in `src/Wissen.Web/appsettings.json` vorübergehend `"RegistrierungErlaubt": true` eintragen und neu starten. Ein E-Mail-Versand ist nicht eingerichtet; der Bestätigungslink wird nach der Registrierung direkt angezeigt. Danach den Eintrag wieder entfernen.

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

7. Für das erste Konto in `/etc/wissen/wissen.env` vorübergehend `RegistrierungErlaubt='true'` setzen, `sudo systemctl restart wissen`, registrieren und bestätigen, dann wieder auf `false` stellen und neu starten.

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
| `deploy/` | Vorlagen für den Server: systemd-Dienst, Umgebungsdatei, Caddyfile, tägliche Sicherung |
| `Dokument/` | Arbeitsbericht als mdBook, siehe [Dokument/README.md](Dokument/README.md) |

## Lizenz

[MIT](LICENSE)
