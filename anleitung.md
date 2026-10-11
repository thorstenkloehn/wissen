# Anleitung: wissen auf Ubuntu Server 24.04 mit nginx

Diese Anleitung beschreibt, wie die Anwendung auf einem eigenen Server mit Ubuntu Server 24.04 LTS läuft. Die App läuft als systemd-Dienst und lauscht nur lokal auf `127.0.0.1:5000`; davor steht nginx als Reverse-Proxy und liefert die Seite per HTTPS aus. Das Zertifikat kommt von Let's Encrypt (certbot).

Die Anleitung ersetzt den Abschnitt mit Caddy in `README.md`: Statt `deploy/Caddyfile` wird die nginx-Konfiguration aus Schritt 8 benutzt. Alle anderen Vorlagen in `deploy/` gelten unverändert.

In den Befehlen sind zu ersetzen:

| Platzhalter | Bedeutung |
| --- | --- |
| `IHRE-DOMAIN.de` | Domain, unter der die Seite erreichbar sein soll |
| `BENUTZER` | Ihr Benutzer auf dem Server (mit `sudo`-Rechten) |
| `IHR_SERVER_PASSWORT` | Passwort der Datenbankrolle auf dem Server |
| `name@example.org` | E-Mail-Adresse des ersten Kontos |

## Teil 1: Vorbereitung

### Was vorher vorhanden sein muss

- Ein Server mit Ubuntu Server 24.04 LTS und ein Benutzer mit `sudo`-Rechten, erreichbar per SSH.
- Eine Domain, deren DNS-Eintrag (A, bei IPv6 auch AAAA) auf die Adresse des Servers zeigt. Ohne das stellt Let's Encrypt kein Zertifikat aus.
- Die Ports 80 und 443 sind von außen erreichbar (auch in der Firewall des Anbieters).
- Auf dem eigenen Rechner: das Repository und das .NET 10 SDK. Auf dem Server wird nicht gebaut.

Prüfen, ob die Domain schon auf den Server zeigt:

```bash
dig +short IHRE-DOMAIN.de
```

### 1. Server aktualisieren und Firewall einrichten

Auf dem Server:

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y ufw rsync

sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

`OpenSSH` muss vor `ufw enable` erlaubt sein, sonst sperren Sie sich aus. Port 5000 und der PostgreSQL-Port 5432 werden nicht geöffnet: Beide sind nur lokal erreichbar.

### 2. Programme installieren

```bash
sudo apt install -y aspnetcore-runtime-10.0 postgresql nginx certbot python3-certbot-nginx
```

- `aspnetcore-runtime-10.0` führt die App und die Konsolenanwendung aus. Findet `apt` das Paket nicht, vorher die Paketquelle für .NET hinzufügen und den Befehl wiederholen:

  ```bash
  sudo add-apt-repository ppa:dotnet/backports
  sudo apt update
  ```

- `postgresql` bringt unter Ubuntu 24.04 PostgreSQL 16 mit, dazu `pg_dump` und `pg_restore` für die Sicherung.

Prüfen:

```bash
dotnet --list-runtimes     # muss Microsoft.AspNetCore.App 10.x zeigen
which dotnet               # muss /usr/bin/dotnet sein (so steht es in wissen.service)
pg_dump --version
systemctl is-active postgresql nginx
```

### 3. Dienstbenutzer und Datenbank anlegen

```bash
sudo useradd --system --no-create-home wissen
sudo -u postgres createuser --pwprompt wissen
sudo -u postgres createdb --owner wissen wissen
```

`createuser` fragt das Passwort der Datenbankrolle ab. Ein eigenes Passwort wählen, nicht das der lokalen Entwicklungsdatenbank, und ohne einfaches Anführungszeichen (`'`), weil es in der Umgebungsdatei in einfachen Anführungszeichen steht.

### 4. Anwendung bauen und auf den Server kopieren

Auf dem eigenen Rechner, im Ordner des Repositorys:

```bash
dotnet publish src/Wissen.Web -c Release -o publish/web
dotnet publish src/Wissen.Cli -c Release -o publish/cli

rsync -a --delete publish/web publish/cli deploy BENUTZER@IHRE-DOMAIN.de:wissen-upload/
```

`appsettings.json` wird nicht mit veröffentlicht, weil sie das Passwort der lokalen Datenbank enthält. Auf dem Server kommen alle Einstellungen aus der Umgebungsdatei (Schritt 5).

Auf dem Server die Dateien an ihren Platz legen:

```bash
sudo mkdir -p /opt/wissen
sudo rsync -a --delete ~/wissen-upload/web/ /opt/wissen/web/
sudo rsync -a --delete ~/wissen-upload/cli/ /opt/wissen/cli/
sudo chown -R root:root /opt/wissen
sudo chmod -R a+rX /opt/wissen
```

## Teil 2: Einrichten

### 5. Umgebungsdatei anlegen

```bash
sudo mkdir -p /etc/wissen
sudo cp ~/wissen-upload/deploy/wissen.env.example /etc/wissen/wissen.env
sudo chown root:root /etc/wissen/wissen.env
sudo chmod 600 /etc/wissen/wissen.env
sudo nano /etc/wissen/wissen.env
```

In der Datei eintragen:

- `AllowedHosts='IHRE-DOMAIN.de'`
- in `ConnectionStrings__DefaultConnection` das Passwort aus Schritt 3 anstelle von `IHR_SERVER_PASSWORT`

Die übrigen Zeilen bleiben, wie sie sind. Wichtig für nginx ist `ASPNETCORE_FORWARDEDHEADERS_ENABLED='true'`: Nur damit sieht die App die Adresse des Besuchers statt der von nginx. Ohne die Zeile teilten sich alle Besucher die Grenze von 5 Anmeldeversuchen in 5 Minuten.

### 6. Datenbanktabellen anlegen

```bash
sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll migrate'
```

Scheitert der Befehl mit `28P01`, stimmt das Passwort in der Umgebungsdatei nicht mit dem der Datenbankrolle überein.

### 7. Dienst starten

```bash
sudo cp ~/wissen-upload/deploy/wissen.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now wissen
systemctl status wissen
```

Prüfen, ob die App lokal antwortet:

```bash
curl -I -H 'Host: IHRE-DOMAIN.de' -H 'X-Forwarded-Proto: https' http://127.0.0.1:5000/doc
```

Erwartet wird `HTTP/1.1 200 OK` (oder `404`, solange die Seite `doc` noch nicht angelegt ist). Ohne den Kopf `X-Forwarded-Proto` antwortet die App mit einer Weiterleitung auf HTTPS, ohne den richtigen `Host` mit `400`.

### 8. nginx einrichten

Die Datei `/etc/nginx/sites-available/wissen` anlegen:

```bash
sudo nano /etc/nginx/sites-available/wissen
```

Inhalt:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name IHRE-DOMAIN.de;

    # Eine Seite darf 200 000 Zeichen Markdown haben; als Formular gesendet
    # ist das mehr als die Voreinstellung von 1 MB.
    client_max_body_size 4m;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;

        # Die App beantwortet nur Anfragen an die Domain aus AllowedHosts.
        proxy_set_header Host $host;
        # Adresse des Besuchers, für die Begrenzung der Anmeldeversuche.
        proxy_set_header X-Forwarded-For $remote_addr;
        # Damit die App weiß, dass der Besucher per HTTPS kommt.
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Einschalten und die Beispielseite von nginx abschalten:

```bash
sudo ln -s /etc/nginx/sites-available/wissen /etc/nginx/sites-enabled/wissen
sudo rm /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl reload nginx
```

`nginx -t` prüft die Konfiguration; nur bei `syntax is ok` neu laden.

Die Sicherheits-Header (`Content-Security-Policy`, `X-Frame-Options` und die anderen) setzt die App selbst. In nginx nicht noch einmal setzen, sonst stehen sie doppelt in der Antwort.

### 9. HTTPS-Zertifikat holen

```bash
sudo certbot --nginx -d IHRE-DOMAIN.de
```

certbot fragt nach einer E-Mail-Adresse für Hinweise zum Ablauf, holt das Zertifikat und trägt in `/etc/nginx/sites-available/wissen` selbst den Block für Port 443 und die Weiterleitung von HTTP auf HTTPS ein. Die Zeilen aus Schritt 8 bleiben erhalten.

Das Zertifikat wird automatisch verlängert. Prüfen:

```bash
systemctl list-timers certbot
sudo certbot renew --dry-run
```

### 10. Erstes Konto anlegen

Die Registrierung im Browser ist abgeschaltet; Konten legt die Konsolenanwendung an. Das Passwort (mindestens 12 Zeichen) wird abgefragt und nicht angezeigt:

```bash
sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll konto-anlegen name@example.org --admin'
```

Danach im Browser `https://IHRE-DOMAIN.de` öffnen und anmelden.

### 11. Tägliche Sicherung einschalten

```bash
sudo cp ~/wissen-upload/deploy/wissen-backup.service ~/wissen-upload/deploy/wissen-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now wissen-backup.timer
sudo systemctl start wissen-backup    # einmal sofort sichern
systemctl status wissen-backup        # Ergebnis ansehen
systemctl list-timers wissen-backup   # nächster Lauf
```

Die Sicherung läuft jede Nacht gegen 3 Uhr nach `/var/lib/wissen/backups`; Sicherungen, die älter als 30 Tage sind, werden gelöscht. Sie liegen auf demselben Server wie die Datenbank und schützen nicht vor dessen Verlust. Deshalb regelmäßig auf einen anderen Rechner holen:

```bash
rsync -a --rsync-path="sudo rsync" BENUTZER@IHRE-DOMAIN.de:/var/lib/wissen/backups/ ~/wissen-backups/
```

Wiederherstellen (überschreibt die Datenbank):

```bash
sudo systemctl stop wissen
sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll restore /var/lib/wissen/backups/DATEI.dump'
sudo systemctl start wissen
```

## Teil 3: Betrieb

### Neue Version einspielen

Auf dem eigenen Rechner wie in Schritt 4 bauen und nach `wissen-upload/` kopieren. Dann auf dem Server:

```bash
sudo systemctl start wissen-backup    # vorher sichern
sudo systemctl stop wissen
sudo rsync -a --delete ~/wissen-upload/web/ /opt/wissen/web/
sudo rsync -a --delete ~/wissen-upload/cli/ /opt/wissen/cli/
sudo chown -R root:root /opt/wissen
sudo chmod -R a+rX /opt/wissen
sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll migrate'
sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll seiten-rendern'
sudo systemctl start wissen
```

`seiten-rendern` erzeugt das HTML aller Seiten und Versionen neu aus dem Markdown. Das ist nach jeder Änderung am `MarkdownRenderer` nötig, sonst gilt sie nicht für schon gespeicherte Seiten.

### Konten verwalten

Alle Befehle der Konsolenanwendung laufen auf dem Server nach demselben Muster:

```bash
sudo sh -c 'set -a; . /etc/wissen/wissen.env; dotnet /opt/wissen/cli/Wissen.Cli.dll BEFEHL'
```

| `BEFEHL` | Zweck |
| --- | --- |
| `konto-anlegen name@example.org [--admin]` | Konto anlegen |
| `konto-admin name@example.org [--entziehen]` | Rolle Administrator geben oder wegnehmen |
| `konto-passwort name@example.org` | neues Passwort setzen |
| `konto-sperren name@example.org [--aufheben]` | Konto sperren oder Sperre aufheben |
| `help` | alle Befehle anzeigen |

### Fehlersuche

| Anzeichen | Ursache und Abhilfe |
| --- | --- |
| `502 Bad Gateway` | Der Dienst läuft nicht: `systemctl status wissen`, Meldungen mit `journalctl -u wissen -n 50` |
| `400 Bad Request` | Die Domain fehlt in `AllowedHosts`, oder in nginx fehlt `proxy_set_header Host $host;` |
| Endlose Weiterleitung im Browser | In nginx fehlt `proxy_set_header X-Forwarded-Proto $scheme;`, oder `ASPNETCORE_FORWARDEDHEADERS_ENABLED` fehlt in der Umgebungsdatei |
| `429` beim Anmelden schon nach wenigen Versuchen aller Besucher zusammen | `X-Forwarded-For` kommt nicht an, siehe Zeile darüber |
| `413 Request Entity Too Large` beim Speichern einer langen Seite | `client_max_body_size` in nginx fehlt oder ist zu klein |
| `28P01` im Protokoll | Passwort in `/etc/wissen/wissen.env` passt nicht zur Datenbankrolle |
| Nach jedem Neustart sind alle abgemeldet | `StateDirectory=wissen` und `Environment=HOME=/var/lib/wissen` fehlen in `wissen.service` |
| certbot meldet einen Fehler bei der Prüfung | DNS zeigt noch nicht auf den Server, oder Port 80 ist nicht offen |

Protokolle:

```bash
journalctl -u wissen -f                  # die App
sudo tail -f /var/log/nginx/error.log    # nginx
```

Nach einer Änderung an `/etc/wissen/wissen.env` den Dienst neu starten: `sudo systemctl restart wissen`. Nach einer Änderung an der nginx-Konfiguration: `sudo nginx -t && sudo systemctl reload nginx`.
