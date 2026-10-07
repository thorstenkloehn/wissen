using Wissen.Infrastructure.Backup;

namespace Wissen.Cli.Modules;

public class RestoreModule(DatabaseBackup backup) : ICommandModule
{
    private const string ConfirmOption = "--ja";

    public string Name => "restore";

    public string Usage => $"restore <Datei> [{ConfirmOption}]";

    public string Description => $"Stellt die Datenbank aus einer Sicherung wieder her und überschreibt den aktuellen Stand ({ConfirmOption}: ohne Rückfrage).";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var confirmed = args.Contains(ConfirmOption);
        var files = args.Where(a => a != ConfirmOption).ToArray();
        if (files.Length != 1)
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        var file = Path.GetFullPath(files[0]);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"Die Sicherungsdatei {file} wurde nicht gefunden.");
            return 1;
        }

        if (!confirmed)
        {
            Console.Write($"Die Datenbank »{backup.Database}« auf {backup.Host} wird mit dem Stand aus {file} überschrieben. Fortfahren? (ja/nein) ");
            if (!string.Equals(Console.ReadLine()?.Trim(), "ja", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Abgebrochen.");
                return 1;
            }
        }

        var exitCode = await backup.RestoreAsync(file, cancellationToken);
        if (exitCode != 0)
        {
            Console.Error.WriteLine($"Die Wiederherstellung ist fehlgeschlagen (pg_restore, Exit-Code {exitCode}). Die Datenbank wurde nicht verändert.");
            return exitCode;
        }

        Console.WriteLine($"Datenbank »{backup.Database}« aus {file} wiederhergestellt.");
        return 0;
    }
}
