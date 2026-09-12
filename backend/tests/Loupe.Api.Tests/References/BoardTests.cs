// Acceptance tests. Traces to L2-017, L2-029, L2-037, L2-038.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class BoardTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_017_Create_private_boards_and_reopen_them_in_alphabetical_order()
    {
        var subject = Guid.NewGuid().ToString();
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            using var other = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
            using var first = await owner.PostAsJsonAsync("/api/boards", new { name = " Window light " });
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            using var second = await owner.PostAsJsonAsync("/api/boards", new { name = "Colour studies" });
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            using var foreign = await other.PostAsJsonAsync("/api/boards", new { name = "Private board" });
            Assert.Equal(HttpStatusCode.Created, foreign.StatusCode);
        }
        await using var reopened = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var reader = await reopened.CreateAuthenticatedClientAsync(subject);
        var boards = await reader.GetFromJsonAsync<JsonElement>("/api/boards");
        Assert.Equal(new[] { "Colour studies", "Window light" }, boards.EnumerateArray().Select(b => b.GetProperty("name").GetString()));
        Assert.All(boards.EnumerateArray(), b => Assert.Equal(0, b.GetProperty("referenceCount").GetInt32()));
    }

    [Fact]
    public async Task L2_017_Duplicate_names_are_normalized_and_scoped_to_the_owner()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var other = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var first = await owner.PostAsJsonAsync("/api/boards", new { name = "Café" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await owner.PostAsJsonAsync("/api/boards", new { name = " CAFE\u0301 " });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("board_name_conflict", (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var independent = await other.PostAsJsonAsync("/api/boards", new { name = "Café" });
        Assert.Equal(HttpStatusCode.Created, independent.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(81)]
    public async Task L2_017_Invalid_names_cannot_create_boards(int length)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.PostAsJsonAsync("/api/boards", new { name = new string('x', length) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("name", out _));
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>("/api/boards")).EnumerateArray());
    }

    [Fact]
    public async Task L2_038_Anonymous_clients_cannot_read_or_create_boards()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var visitor = factory.CreateClient();
        using var read = await visitor.GetAsync("/api/boards");
        using var create = await visitor.PostAsJsonAsync("/api/boards", new { name = "Board" });
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }
}
