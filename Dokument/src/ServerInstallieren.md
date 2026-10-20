# Server installieren

## Nginx installieren
```
sudo apt install snapd
sudo apt-get install nginx
sudo rm /etc/nginx/sites-enabled/default
sudo systemctl stop nginx
sudo snap install --classic certbot
sudo ln -s /snap/bin/certbot /usr/bin/certbot
sudo systemctl stop nginx
sudo certbot certonly --manual --preferred-challenges dns \
  -d *.wissen-ahrensburg.de -d wissen-ahrensburg.de
```

Certificate is saved at: /etc/letsencrypt/live/wissen-ahrensburg.de/fullchain.pem  
Key is saved at:         /etc/letsencrypt/live/wissen-ahrensburg.de/privkey.pem

## PostgreSQL installieren
```
# Import the repository signing key:
sudo apt install curl ca-certificates
sudo install -d /usr/share/postgresql-common/pgdg
sudo curl -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc --fail https://www.postgresql.org/media/keys/ACCC4CF8.asc

# Create the repository configuration file:
. /etc/os-release
sudo sh -c "echo 'deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt $VERSION_CODENAME-pgdg main' > /etc/apt/sources.list.d/pgdg.list"

# Update the package lists:
sudo apt update

# PostgreSQL installieren
sudo apt install postgresql-18

# PostGIS-Erweiterung installieren
sudo apt-get install postgis postgresql-18-postgis-3 postgresql-18
sudo -u postgres -i
createuser thorsten
createdb -E UTF8 -O thorsten thorsten
psql -d thorsten -c "CREATE EXTENSION postgis;"
psql -d thorsten -c "CREATE EXTENSION hstore;"
psql -d thorsten -c "ALTER TABLE geometry_columns OWNER TO thorsten;"
psql -d thorsten -c "ALTER TABLE spatial_ref_sys OWNER TO thorsten;"
psql -d thorsten -c "\password thorsten"
psql -c "ALTER USER thorsten CREATEDB;"
exit
cd $HOME
wget https://download.geofabrik.de/europe/germany/schleswig-holstein-latest.osm.pbf
sudo apt-get install osmosis osm2pgsql
osmosis --read-pbf file=schleswig-holstein-latest.osm.pbf --bounding-box left=10.1141 right=10.3716 top=53.7136 bottom=53.6249 --write-pbf file=ahrensburg.pbf
osm2pgsql -d thorsten -H localhost -U thorsten --create -G --hstore -W ahrensburg.pbf
```

sudo nano /etc/nginx/conf.d/start.conf
```
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name dienst.wissen-ahrensburg.de;
    ssl_certificate /etc/letsencrypt/live/wissen-ahrensburg.de/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/wissen-ahrensburg.de/privkey.pem;

    # eigentliche Backend-Anbindung (proxy_pass, fastcgi_pass, root, …)
}

# HTTP auf HTTPS umleiten
server {
    listen 80;
    listen [::]:80;
    server_name dienst.wissen-ahrensburg.de;
    return 301 https://$host$request_uri;
}
```

## Server aktualisieren und Firewall einrichten
Auf dem Server:
```
sudo apt update && sudo apt upgrade -y
sudo apt install -y ufw rsync

sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

## Programme installieren
aspnetcore-runtime-10.0 führt die App und die Konsolenanwendung aus. Findet apt das Paket nicht, vorher die Paketquelle für .NET hinzufügen und den Befehl wiederholen:
```
sudo apt install -y aspnetcore-runtime-10.0
```

## Anwendung bauen und auf den Server kopieren
Auf dem eigenen Rechner, im Ordner des Repositorys:
```
dotnet publish src/Wissen.Web -c Release -o publish/web
dotnet publish src/Wissen.Cli -c Release -o publish/cli

rsync -a --delete publish/web publish/cli deploy BENUTZER@IHRE-DOMAIN.de:wissen-upload/
```

appsettings.json wird nicht mit veröffentlicht, weil sie das Passwort der lokalen Datenbank enthält. Auf dem Server kommen alle Einstellungen aus der Umgebungsdatei (Schritt 5).

Auf dem Server die Dateien an ihren Platz legen:

```
sudo mkdir -p /opt/wissen
sudo rsync -a --delete ~/wissen-upload/web/ /opt/wissen/web/
sudo rsync -a --delete ~/wissen-upload/cli/ /opt/wissen/cli/
sudo chown -R root:root /opt/wissen
sudo chmod -R a+rX /opt/wissen
```

## Teil 2: Einrichten
Umgebungsdatei anlegen:
```
sudo mkdir -p /etc/wissen
sudo cp ~/wissen-upload/deploy/wissen.env.example /etc/wissen/wissen.env
sudo chown root:root /etc/wissen/wissen.env
sudo chmod 600 /etc/wissen/wissen.env
sudo nano /etc/wissen/wissen.env
```

In der Datei eintragen:

- AllowedHosts='IHRE-DOMAIN.de'
- in ConnectionStrings__DefaultConnection das Passwort aus Schritt 3 anstelle von IHR_SERVER_PASSWORT

Die übrigen Zeilen bleiben, wie sie sind. Wichtig für nginx ist ASPNETCORE_FORWARDEDHEADERS_ENABLED='true': Nur damit sieht die App die Adresse des Besuchers statt der von nginx. Ohne die Zeile teilten sich alle Besucher die Grenze von 5 Anmeldeversuchen in 5 Minuten.
