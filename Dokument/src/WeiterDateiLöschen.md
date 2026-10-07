# Weitere Datei löschen

Beim Projekt Wissen.Web wird in Program.cs die Zeile gelöscht, weil wir andere Router nutzen:
```
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
```
Ich habe die HomeController-Datei gelöscht.
