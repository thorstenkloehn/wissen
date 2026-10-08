using System.Text;
using Wissen.Infrastructure.Identity;

namespace Wissen.Cli.Modules;

public class KontoAnlegenModule(KontoAnlage konten) : ICommandModule
{
    public string Name => "konto-anlegen";

    public string Usage => "konto-anlegen <E-Mail>";

    public string Description => "Legt ein Konto an, das sich sofort anmelden kann; das Passwort wird abgefragt.";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        // Das Passwort ist nie ein Argument: Es stünde in der Prozessliste und im Verlauf der Shell.
        var passwort = ReadPassword("Passwort: ");
        if (!Console.IsInputRedirected && passwort != ReadPassword("Passwort wiederholen: "))
        {
            Console.Error.WriteLine("Die Passwörter stimmen nicht überein. Es wurde kein Konto angelegt.");
            return 1;
        }

        var result = await konten.AnlegenAsync(args[0], passwort);
        if (!result.Succeeded)
        {
            Console.Error.WriteLine("Es wurde kein Konto angelegt:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  {error.Description}");
            }
            return 1;
        }

        Console.WriteLine($"Konto »{args[0].Trim()}« angelegt.");
        return 0;
    }

    // Liest ohne Anzeige der Eingabe. Aus einer Pipe oder Datei wird eine Zeile gelesen.
    private static string ReadPassword(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? "";
        }

        Console.Write(prompt);
        var passwort = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return passwort.ToString();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (passwort.Length > 0)
                {
                    passwort.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                passwort.Append(key.KeyChar);
            }
        }
    }
}
