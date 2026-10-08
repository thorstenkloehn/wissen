using System.Xml;
using System.Xml.Linq;
using Wissen.Infrastructure.Backup;
using Wissen.Infrastructure.Models;

namespace Wissen.Cli.Modules;

public class RestoreXmlModule(SeitenXmlImport import) : ICommandModule
{
    private const string ConfirmOption = "--ja";

    public string Name => "restore-xml";

    public string Usage => $"restore-xml <Datei> [{ConfirmOption}]";

    public string Description => $"Liest Seiten aus einer XML-Sicherung ein; vorhandene Seiten mit gleichem Pfad werden ersetzt ({ConfirmOption}: ohne Rückfrage).";

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
            Console.Error.WriteLine($"Die XML-Datei {file} wurde nicht gefunden.");
            return 1;
        }

        IReadOnlyList<Seite> seiten;
        try
        {
            seiten = import.Read(XDocument.Load(file));
        }
        catch (Exception e) when (e is XmlException or FormatException)
        {
            Console.Error.WriteLine($"Die XML-Datei {file} lässt sich nicht einlesen: {e.Message} Die Datenbank wurde nicht verändert.");
            return 1;
        }

        var vorhanden = await import.FindExistingPathsAsync(seiten, cancellationToken);
        if (vorhanden.Count > 0 && !confirmed)
        {
            Console.WriteLine($"{vorhanden.Count} vorhandene Seite(n) werden samt Versionsgeschichte durch den Stand aus der Datei ersetzt:");
            foreach (var path in vorhanden)
            {
                Console.WriteLine($"  {path}");
            }
            Console.Write("Fortfahren? (ja/nein) ");
            if (!string.Equals(Console.ReadLine()?.Trim(), "ja", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Abgebrochen.");
                return 1;
            }
        }

        var result = await import.ImportAsync(seiten, cancellationToken);
        Console.WriteLine($"{seiten.Count} Seite(n) aus {file} eingelesen: {result.Neu} neu, {result.Ersetzt} ersetzt.");
        return 0;
    }
}
