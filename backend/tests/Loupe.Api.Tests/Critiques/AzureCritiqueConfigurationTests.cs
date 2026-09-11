// Acceptance Test. Traces to: L2-036, L2-050.
// Missing Azure configuration disables admission; invalid endpoints fail startup safely.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class AzureCritiqueConfigurationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("Ai:Endpoint", null)]
    [InlineData("Ai:Endpoint", " ")]
    [InlineData("Ai:Deployment", null)]
    [InlineData("Ai:Deployment", " ")]
    [InlineData("Ai:ApiKey", null)]
    [InlineData("Ai:ApiKey", " ")]
    public async Task Missing_configuration_keeps_manual_work_available_without_admitting_critique(string setting, string? value)
    {
        var settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" };
        settings[setting] = value;
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = settings };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("integration_not_configured", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var saved = await client.GetAsync($"/api/photographs/{id}");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
        deleted.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/relative")]
    [InlineData("http://loupe-fixture.openai.azure.com")]
    [InlineData("https://user:private-secret@loupe-fixture.openai.azure.com")]
    [InlineData("https://loupe-fixture.openai.azure.com?secret=private-secret")]
    [InlineData("https://loupe-fixture.openai.azure.com#private-secret")]
    [InlineData("https://loupe-fixture.openai.azure.com/openai/v1")]
    public async Task Invalid_endpoint_is_rejected_without_echoing_configuration(string endpoint)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:Endpoint"] = endpoint, ["Ai:ApiKey"] = "fixture-only-key" } };
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("Ai:Endpoint", error.Message);
        Assert.DoesNotContain(endpoint, error.Message);
        Assert.DoesNotContain("fixture-only-key", error.Message);
    }
}
