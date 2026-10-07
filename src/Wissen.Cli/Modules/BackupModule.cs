using Wissen.Infrastructure.Backup;

namespace Wissen.Cli.Modules;

public class BackupModule(DatabaseBackup backup) : ICommandModule
{
    public string Name => "backup";

    public string Usage => "backup [Datei]";

    public string Description => "Sichert die Datenbank in eine Datei (Standard: backups/<Datenbank>-<Zeitstempel>.dump).";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        var file = Path.GetFullPath(args.Length == 1
            ? args[0]
            : Path.Combine("backups", $"{backup.Database}-{DateTime.Now:yyyyMMdd-HHmmss}.dump"));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var exitCode = await backup.BackupAsync(file, cancellationToken);
        if (exitCode != 0)
        {
            Console.Error.WriteLine($"Die Sicherung ist fehlgeschlagen (pg_dump, Exit-Code {exitCode}).");
            return exitCode;
        }

        Console.WriteLine($"Datenbank »{backup.Database}« gesichert nach {file} ({new FileInfo(file).Length:N0} Bytes).");
        return 0;
    }
}
