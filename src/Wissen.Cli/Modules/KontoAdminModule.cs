using Wissen.Infrastructure.Identity;

namespace Wissen.Cli.Modules;

public class KontoAdminModule(KontoAnlage konten) : ICommandModule
{
    private const string RevokeOption = "--entziehen";

    public string Name => "konto-admin";

    public string Usage => $"konto-admin <E-Mail> [{RevokeOption}]";

    public string Description => $"Gibt einem Konto die Rolle Administrator (darf Seiten löschen); {RevokeOption} nimmt sie wieder weg.";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var revoke = args.Contains(RevokeOption);
        var emails = args.Where(a => a != RevokeOption).ToArray();
        if (emails.Length != 1)
        {
            Console.Error.WriteLine($"Aufruf: {Usage}");
            return 2;
        }

        var email = emails[0].Trim();
        var result = await konten.AdministratorSetzenAsync(email, !revoke);
        if (result is null)
        {
            Console.Error.WriteLine($"Es gibt kein Konto mit der E-Mail-Adresse »{email}«.");
            return 1;
        }
        if (!result.Succeeded)
        {
            Console.Error.WriteLine("Die Rolle wurde nicht geändert:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  {error.Description}");
            }
            return 1;
        }

        Console.WriteLine(revoke
            ? $"Das Konto »{email}« ist kein Administrator mehr."
            : $"Das Konto »{email}« ist jetzt Administrator.");
        Console.WriteLine("Die Änderung gilt ab der nächsten Anmeldung.");
        return 0;
    }
}
