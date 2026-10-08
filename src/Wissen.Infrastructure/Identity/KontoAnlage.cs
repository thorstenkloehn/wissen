using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Wissen.Infrastructure.Identity;

// Legt ein Konto an, das sich sofort anmelden kann. Die Konsolenanwendung nutzt das statt der
// Registrierung im Browser: Ein E-Mail-Versand ist nicht eingerichtet, und wer die Konsole
// bedienen darf, braucht keine Bestätigung per E-Mail.
public class KontoAnlage(UserManager<IdentityUser> userManager)
{
    public async Task<IdentityResult> AnlegenAsync(string email, string passwort)
    {
        email = email.Trim();
        if (!new EmailAddressAttribute().IsValid(email))
        {
            return IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email));
        }
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return IdentityResult.Failed(userManager.ErrorDescriber.DuplicateEmail(email));
        }

        // Wie auf der Registrierungsseite ist die E-Mail-Adresse zugleich der Benutzername.
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        return await userManager.CreateAsync(user, passwort);
    }
}
