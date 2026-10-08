using Wissen.Infrastructure.Rendering;

namespace Wissen.Cli.Modules;

public class SeitenRendernModule(SeitenRendern rendern) : ICommandModule
{
    private const string CheckOption = "--pruefen";

    public string Name => "seiten-rendern";

    public string Usage => $"seiten-rendern [{CheckOption}]";

    public string Description => $"Erzeugt das HTML aller Seiten und Versionen neu aus dem Markdown ({CheckOption}: nur zählen, nichts ändern).";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var check = args.Contains(CheckOption);
        if (args.Any(a => a != CheckOption))
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        var result = await rendern.RunAsync(speichern: !check, cancellationToken);
        Console.WriteLine(check
            ? $"{result.Seiten} Seite(n) und {result.Versionen} Version(en) geprüft: Bei {result.SeitenGeaendert} Seite(n) und {result.VersionenGeaendert} Version(en) würde sich das HTML ändern. Es wurde nichts geändert."
            : $"{result.Seiten} Seite(n) und {result.Versionen} Version(en) neu erzeugt: Bei {result.SeitenGeaendert} Seite(n) und {result.VersionenGeaendert} Version(en) hat sich das HTML geändert.");
        if (result.Fehler.Count == 0)
        {
            return 0;
        }

        Console.Error.WriteLine($"{result.Fehler.Count} Stand/Stände ließen sich nicht umsetzen und behalten ihr bisheriges HTML:");
        foreach (var fehler in result.Fehler)
        {
            Console.Error.WriteLine($"  {fehler}");
        }
        return 1;
    }
}
