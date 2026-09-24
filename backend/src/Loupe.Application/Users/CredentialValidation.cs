using System.Net.Mail;
using Loupe.Application.Common;
namespace Loupe.Application.Users;

public static class CredentialValidation
{
    public static bool IsEmail(string? email)
    {
        var value = email?.Trim() ?? "";
        return value.Length <= 254 && MailAddress.TryCreate(value, out var parsed) && parsed.Address == value && value.Contains('@');
    }
    public static bool IsPassword(string? password) => password is not null && password.EnumerateRunes().Count() is >= 8 and <= 128;
    public static bool IsName(string? name) => (name?.Trim() ?? "").EnumerateRunes().Count() is >= 1 and <= 200;
    public static string Email(string? email)
    {
        if (!IsEmail(email)) throw new RequestValidationException("email", "Enter a valid email address.");
        return email!.Trim();
    }
    public static void Password(string? password)
    {
        if (!IsPassword(password)) throw new RequestValidationException("password", "Use a password with 8 to 128 characters.");
    }
    public static string Name(string? name)
    {
        if (!IsName(name)) throw new RequestValidationException("name", "Use a display name with 1 to 200 characters.");
        return name!.Trim();
    }
}
