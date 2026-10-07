using Microsoft.AspNetCore.Identity;

namespace Wissen.Web.Areas.Identity;

public class GermanIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Ein unbekannter Fehler ist aufgetreten.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Der Datensatz wurde zwischenzeitlich geändert. Bitte versuchen Sie es erneut.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Falsches Passwort.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Ungültiges Token.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), "Der Wiederherstellungscode konnte nicht eingelöst werden.");
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "Ein Benutzer mit dieser Anmeldung ist bereits vorhanden.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), $"Der Benutzername '{userName}' ist ungültig. Er darf nur Buchstaben und Ziffern enthalten.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), $"Die E-Mail-Adresse '{email}' ist ungültig.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), $"Der Benutzername '{userName}' ist bereits vergeben.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), $"Die E-Mail-Adresse '{email}' ist bereits vergeben.");
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), $"Der Rollenname '{role}' ist ungültig.");
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), $"Der Rollenname '{role}' ist bereits vergeben.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "Für den Benutzer ist bereits ein Passwort festgelegt.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "Die Sperrung ist für diesen Benutzer nicht aktiviert.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), $"Der Benutzer hat die Rolle '{role}' bereits.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), $"Der Benutzer hat die Rolle '{role}' nicht.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Passwörter müssen mindestens {length} Zeichen lang sein.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Passwörter müssen mindestens {uniqueChars} verschiedene Zeichen enthalten.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Passwörter müssen mindestens ein Sonderzeichen enthalten.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Passwörter müssen mindestens eine Ziffer ('0'-'9') enthalten.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Passwörter müssen mindestens einen Kleinbuchstaben ('a'-'z') enthalten.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Passwörter müssen mindestens einen Großbuchstaben ('A'-'Z') enthalten.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
