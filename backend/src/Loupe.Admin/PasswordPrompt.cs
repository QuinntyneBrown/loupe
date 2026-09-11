using System.Text;
using Loupe.Application.Common;
namespace Loupe.Admin;

public static class PasswordPrompt
{
    public static string Read()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        Console.Error.Write("Password: ");
        var password = HiddenLine();
        Console.Error.Write("Confirm password: ");
        if (password != HiddenLine()) throw new RequestValidationException("password", "Passwords do not match.");
        return password;
    }
    private static string HiddenLine()
    {
        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); return text.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (text.Length > 0) text.Length--; }
            else if (!char.IsControl(key.KeyChar)) text.Append(key.KeyChar);
        }
    }
}
