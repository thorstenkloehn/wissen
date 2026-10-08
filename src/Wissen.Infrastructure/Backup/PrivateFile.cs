namespace Wissen.Infrastructure.Backup;

// Sicherungen enthalten Konten mit Passwort-Hashes und die Versionsgeschichte der Seiten.
// Sie sollen deshalb nur für den Benutzer lesbar sein, der sie anlegt.
public static class PrivateFile
{
    // Legt die Datei leer an (Modus 600) und fehlende Ordner dazu (Modus 700). Wer danach in die
    // Datei schreibt (pg_dump, XmlWriter), überschreibt den Inhalt; die Rechte bleiben erhalten.
    public static void Create(string file)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(file))!;
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            File.Create(file).Dispose();
            return;
        }

        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        new FileStream(file, new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, UnixCreateMode = mode }).Dispose();
        // Eine schon vorhandene Datei behält beim Überschreiben ihre alten Rechte.
        File.SetUnixFileMode(file, mode);
    }
}
