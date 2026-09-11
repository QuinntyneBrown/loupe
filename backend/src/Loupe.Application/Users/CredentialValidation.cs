using System.Net.Mail;
using Loupe.Application.Common;
namespace Loupe.Application.Users;

public static class CredentialValidation
{
    public static string Email(string? email)
    {
        var value = email?.Trim() ?? "";
        if (value.Length > 254 || !MailAddress.TryCreate(value, out var parsed) || parsed.Address != value || !value.Contains('@'))
            throw new RequestValidationException("email", "Enter a valid email address.");
        return value;
    }
    public static void Password(string? password)
    {
        if (password is null || password.EnumerateRunes().Count() is < 15 or > 128)
            throw new RequestValidationException("password", "Use a password with 15 to 128 characters.");
    }
}
