using System.Text;

namespace Wissen.Cli;

// Fragt ein Passwort ab. Es ist nie ein Argument: Dort stünde es in der Prozessliste und im Verlauf der Shell.
public static class PasswortEingabe
{
    // Fragt das Passwort zweimal ab und liefert null, wenn die Eingaben nicht übereinstimmen.
    // Aus einer Pipe oder Datei wird nur eine Zeile gelesen.
    public static string? ReadTwice()
    {
        var passwort = Read("Passwort: ");
        return Console.IsInputRedirected || passwort == Read("Passwort wiederholen: ") ? passwort : null;
    }

    // Liest ohne Anzeige der Eingabe. Aus einer Pipe oder Datei wird eine Zeile gelesen.
    private static string Read(string prompt)
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
