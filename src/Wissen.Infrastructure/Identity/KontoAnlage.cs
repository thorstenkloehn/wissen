using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Wissen.Infrastructure.Identity;

// Legt ein Konto an, das sich sofort anmelden kann. Die Konsolenanwendung nutzt das statt der
// Registrierung im Browser: Ein E-Mail-Versand ist nicht eingerichtet, und wer die Konsole
// bedienen darf, braucht keine Bestätigung per E-Mail.
public class KontoAnlage(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
{
    public async Task<IdentityResult> AnlegenAsync(string email, string passwort, bool administrator = false)
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
        var result = await userManager.CreateAsync(user, passwort);
        return result.Succeeded && administrator ? await AdministratorSetzenAsync(user, true) : result;
    }

    // Gibt einem vorhandenen Konto die Rolle Administrator oder entzieht sie. Liefert null, wenn es
    // kein Konto mit dieser E-Mail-Adresse gibt.
    public async Task<IdentityResult?> AdministratorSetzenAsync(string email, bool administrator)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        return user is null ? null : await AdministratorSetzenAsync(user, administrator);
    }

    private async Task<IdentityResult> AdministratorSetzenAsync(IdentityUser user, bool administrator)
    {
        if (await userManager.IsInRoleAsync(user, Rollen.Administrator) == administrator)
        {
            return IdentityResult.Success;
        }
        if (!administrator)
        {
            return await userManager.RemoveFromRoleAsync(user, Rollen.Administrator);
        }

        if (!await roleManager.RoleExistsAsync(Rollen.Administrator))
        {
            var created = await roleManager.CreateAsync(new IdentityRole(Rollen.Administrator));
            if (!created.Succeeded)
            {
                return created;
            }
        }
        return await userManager.AddToRoleAsync(user, Rollen.Administrator);
    }
}
