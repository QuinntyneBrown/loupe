using Loupe.Application.Users;
namespace Loupe.Api.Accounts;

public sealed class SeedOptions
{
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsConfigured => Email.Length > 0 || Name.Length > 0 || Password.Length > 0;
    public bool HasValidEmail => CredentialValidation.IsEmail(Email);
    public bool HasValidName => CredentialValidation.IsName(Name);
    public bool HasValidPassword => CredentialValidation.IsPassword(Password);
}
