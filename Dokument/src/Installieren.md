# Installieren

Hier berichte ich, wie wir die Anwendung installiert haben.

Erstelle ein neues Projekt. Wir geben das im Terminalfenster vom Ubuntu-Rechner ein:
```
dotnet new mvc --auth Individual -o wissen
cd wissen
```

Wir arbeiten nicht mit SQLite3, mein System läuft mit PostgreSQL zusammen.
PostgreSQL-Pakete installieren:
```
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```
Datenbankname und Benutzer einrichten bei PostgreSQL:
```
sudo -u postgres -i
createuser thorsten
createdb -E UTF8 -O thorsten thorsten
psql -d thorsten -c "\password thorsten"
psql -c "ALTER USER thorsten CREATEDB;"
exit
```
Jetzt ändern wir die Datei appsettings.json, folgende Werte hinzufügen oder ändern:

```
"ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wissen;Username=thorsten;Password=dein_passwort"
  },
```

In der Datei Program.cs Text hinzufügen:

```
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using wissen.Data;

// Connection String abrufen
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ApplicationDbContext auf Npgsql (PostgreSQL) umstellen
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity konfigurieren
builder.Services.AddDefaultIdentity<IdentityUser>(options => {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

```

Jetzt noch migrieren, im Terminal eingeben:

```
# EF Core CLI Tools installieren (falls noch nicht global vorhanden)
dotnet tool install --global dotnet-ef

# Alte Migrationen entfernen (optional, falls vorhanden)
# rm -rf Data/Migrations

# Neue Migration für PostgreSQL anlegen und anwenden
dotnet ef migrations add InitialPostgreMigration
dotnet ef database update
```
Jetzt können wir starten:

```
dotnet run
```
