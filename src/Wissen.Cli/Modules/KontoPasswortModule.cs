using Wissen.Infrastructure.Identity;

namespace Wissen.Cli.Modules;

public class KontoPasswortModule(KontoAnlage konten) : ICommandModule
{
    public string Name => "konto-passwort";

    public string Usage => "konto-passwort <E-Mail>";

    public string Description => "Setzt ein neues Passwort für ein Konto; das Passwort wird abgefragt.";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        var passwort = PasswortEingabe.ReadTwice();
        if (passwort is null)
        {
            Console.Error.WriteLine("Die Passwörter stimmen nicht überein. Das Passwort wurde nicht geändert.");
            return 1;
        }

        var email = args[0].Trim();
        var result = await konten.PasswortSetzenAsync(email, passwort);
        if (result is null)
        {
            Console.Error.WriteLine($"Es gibt kein Konto mit der E-Mail-Adresse »{email}«.");
            return 1;
        }
        if (!result.Succeeded)
        {
            Console.Error.WriteLine("Das Passwort wurde nicht geändert:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  {error.Description}");
            }
            return 1;
        }

        Console.WriteLine($"Das Passwort des Kontos »{email}« wurde geändert.");
        Console.WriteLine("Wer mit dem Konto noch angemeldet ist, wird innerhalb von 30 Minuten abgemeldet.");
        return 0;
    }
}
