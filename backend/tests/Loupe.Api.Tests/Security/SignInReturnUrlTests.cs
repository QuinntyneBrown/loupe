// Acceptance Test
// Traces to: L2-037, L2-039
// Description: Sign-in destinations cannot escape the local application.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Loupe.Api.Tests.Security;

public sealed class SignInReturnUrlTests
{
    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/%2f%2fevil.example")]
    [InlineData("/my-work\r\nLocation: https://evil.example")]
    public async Task L2_037_1_Reject_nonlocal_return_destinations(string returnUrl)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/api/session/sign-in?returnUrl=" + Uri.EscapeDataString(returnUrl));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("returnUrl", out _));
    }
}
