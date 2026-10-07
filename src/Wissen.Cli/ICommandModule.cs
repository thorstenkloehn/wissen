namespace Wissen.Cli;

// Ein Befehl der Konsolenanwendung. Neue Module implementieren diese Schnittstelle
// und werden in Program.cs mit AddScoped<ICommandModule, ...>() angemeldet.
public interface ICommandModule
{
    string Name { get; }

    string Usage { get; }

    string Description { get; }

    Task<int> RunAsync(string[] args, CancellationToken cancellationToken);
}
