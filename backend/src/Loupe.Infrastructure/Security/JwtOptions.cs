namespace Loupe.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Loupe";
    public string Audience { get; set; } = "Loupe";
    public string SigningKey { get; set; } = "";
    public bool HasValidKey
    {
        get { try { return Convert.FromBase64String(SigningKey).Length >= 32; } catch (FormatException) { return false; } }
    }
}
