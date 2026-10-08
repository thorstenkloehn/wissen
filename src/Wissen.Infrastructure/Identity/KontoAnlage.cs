using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Wissen.Infrastructure.Identity;

// Verwaltet Konten für die Konsolenanwendung: anlegen, Rolle, Passwort und Sperre.
// Anlegen erzeugt ein Konto, das sich sofort anmelden kann. Die Konsolenanwendung nutzt das statt der
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

    // Setzt ein neues Passwort; liefert null, wenn es das Konto nicht gibt. Ein E-Mail-Versand für
    // „Passwort vergessen“ ist nicht eingerichtet, deshalb geht das nur über die Konsolenanwendung.
    // Bestehende Anmeldungen des Kontos enden dabei (neuer Sicherheitsstempel).
    public async Task<IdentityResult?> PasswortSetzenAsync(string email, string passwort)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return null;
        }

        // Erst prüfen, dann ersetzen: Sonst stünde das Konto nach einem abgelehnten Passwort ohne Passwort da.
        foreach (var validator in userManager.PasswordValidators)
        {
            var valid = await validator.ValidateAsync(userManager, user, passwort);
            if (!valid.Succeeded)
            {
                return valid;
            }
        }
        if (await userManager.HasPasswordAsync(user))
        {
            var removed = await userManager.RemovePasswordAsync(user);
            if (!removed.Succeeded)
            {
                return removed;
            }
        }
        return await userManager.AddPasswordAsync(user, passwort);
    }

    // Sperrt ein Konto auf Dauer oder hebt die Sperre auf; liefert null, wenn es das Konto nicht gibt.
    // Ein gesperrtes Konto kann sich nicht anmelden. Wer noch angemeldet ist, wird abgemeldet, sobald
    // die Web-App den Sicherheitsstempel das nächste Mal prüft (spätestens nach 30 Minuten).
    public async Task<IdentityResult?> SperrenAsync(string email, bool sperren)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return null;
        }

        var result = await userManager.SetLockoutEnabledAsync(user, true);
        if (result.Succeeded)
        {
            result = await userManager.SetLockoutEndDateAsync(user, sperren ? DateTimeOffset.MaxValue : null);
        }
        if (result.Succeeded)
        {
            result = sperren
                ? await userManager.UpdateSecurityStampAsync(user)
                : await userManager.ResetAccessFailedCountAsync(user);
        }
        return result;
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
