using System.Xml;
using Wissen.Infrastructure.Backup;

namespace Wissen.Cli.Modules;

public class BackupXmlModule(SeitenXmlExport export) : ICommandModule
{
    public string Name => "backup-xml";

    public string Usage => "backup-xml [Datei]";

    public string Description => "Sichert alle Seiten mit Versionsgeschichte als XML (Standard: backups/seiten-<Zeitstempel>.xml).";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        var file = Path.GetFullPath(args.Length == 1
            ? args[0]
            : Path.Combine("backups", $"seiten-{DateTime.Now:yyyyMMdd-HHmmss}.xml"));
        PrivateFile.Create(file);

        var document = await export.CreateAsync(cancellationToken);
        await using (var writer = XmlWriter.Create(file, new XmlWriterSettings { Indent = true, Async = true }))
        {
            await document.SaveAsync(writer, cancellationToken);
        }

        var seiten = document.Root!.Elements("seite").Count();
        var versionen = document.Root.Descendants("version").Count();
        Console.WriteLine($"{seiten} Seite(n) mit {versionen} Version(en) gesichert nach {file} ({new FileInfo(file).Length:N0} Bytes).");
        return 0;
    }
}
