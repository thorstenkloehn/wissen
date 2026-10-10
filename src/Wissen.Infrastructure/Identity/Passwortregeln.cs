namespace Wissen.Infrastructure.Identity;

public static class Passwortregeln
{
    // Mindestlänge neuer Passwörter in Web-App und Konsolenanwendung (Voreinstellung von Identity: 6).
    // Schon vergebene kürzere Passwörter gelten weiter, bis sie geändert werden.
    public const int Mindestlaenge = 12;
}
