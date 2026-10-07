using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;

namespace Wissen.Cli.Modules;

public class MigrateModule(ApplicationDbContext db) : ICommandModule
{
    public string Name => "migrate";

    public string Usage => "migrate";

    public string Description => "Wendet ausstehende EF-Core-Migrationen auf die Datenbank an.";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            Console.WriteLine("Die Datenbank ist auf dem aktuellen Stand.");
            return 0;
        }

        await db.Database.MigrateAsync(cancellationToken);
        Console.WriteLine($"{pending.Count} Migration(en) angewendet:");
        foreach (var migration in pending)
        {
            Console.WriteLine($"  {migration}");
        }
        return 0;
    }
}
