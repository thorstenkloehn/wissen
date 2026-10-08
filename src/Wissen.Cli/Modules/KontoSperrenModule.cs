using Wissen.Infrastructure.Identity;

namespace Wissen.Cli.Modules;

public class KontoSperrenModule(KontoAnlage konten) : ICommandModule
{
    private const string UnlockOption = "--aufheben";

    public string Name => "konto-sperren";

    public string Usage => $"konto-sperren <E-Mail> [{UnlockOption}]";

    public string Description => $"Sperrt ein Konto, sodass es sich nicht mehr anmelden kann; {UnlockOption} hebt die Sperre auf.";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var unlock = args.Contains(UnlockOption);
        var emails = args.Where(a => a != UnlockOption).ToArray();
        if (emails.Length != 1)
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        var email = emails[0].Trim();
        var result = await konten.SperrenAsync(email, !unlock);
        if (result is null)
        {
            Console.Error.WriteLine($"Es gibt kein Konto mit der E-Mail-Adresse »{email}«.");
            return 1;
        }
        if (!result.Succeeded)
        {
            Console.Error.WriteLine("Die Sperre wurde nicht geändert:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  {error.Description}");
            }
            return 1;
        }

        if (unlock)
        {
            Console.WriteLine($"Das Konto »{email}« ist nicht mehr gesperrt.");
        }
        else
        {
            Console.WriteLine($"Das Konto »{email}« ist gesperrt.");
            Console.WriteLine("Wer mit dem Konto noch angemeldet ist, wird innerhalb von 30 Minuten abgemeldet.");
        }
        return 0;
    }
}
