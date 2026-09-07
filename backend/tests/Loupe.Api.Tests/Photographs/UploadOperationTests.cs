// Given repeated or conflicting upload keys, when requests reach real API instances,
// then one durable operation is resolved within 24 hours without duplicate media.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadOperationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_030_1_4_Concurrent_uploads_and_replay_resolve_one_saved_image_across_instances()
    {
        await using var first = new ApiFactory(database.ConnectionString, database.MediaRoot);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var a = await first.CreateAuthenticatedClientAsync(subject);
        using var b = await second.CreateAuthenticatedClientAsync(subject);
        var bytes = ImageBytes();
        var key = Guid.NewGuid().ToString();
        var before = FileCount();
        var responses = await Task.WhenAll(SendAsync(a, key, bytes), SendAsync(b, key, bytes));
        try
        {
            foreach (var response in responses) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var left = await responses[0].Content.ReadFromJsonAsync<JsonElement>();
            var right = await responses[1].Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(left.GetProperty("id").GetGuid(), right.GetProperty("id").GetGuid());
            using var replay = await SendAsync(b, key, bytes, title: "  Still life  ");
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            var again = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(left.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());
            Assert.Equal("Still life", again.GetProperty("title").GetString());
            var list = await b.GetFromJsonAsync<JsonElement>("/api/photographs");
            Assert.Single(list.GetProperty("items").EnumerateArray());
            Assert.Equal(before + 2, FileCount());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("title")]
    [InlineData("intent")]
    [InlineData("bytes")]
    public async Task L2_030_2_Conflicting_payload_does_not_apply_or_retain_new_media(string field)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var key = Guid.NewGuid().ToString();
        var bytes = ImageBytes();
        using var first = await SendAsync(client, key, bytes);
        first.EnsureSuccessStatusCode();
        var before = FileCount();
        using var conflict = await SendAsync(client, key, field == "bytes" ? ImageBytes(9) : bytes,
            field == "title" ? "Changed title" : "Still life", field == "intent" ? "Changed intention" : null);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("operation_conflict", problem.GetProperty("code").GetString());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        var saved = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal("Still life", saved.GetProperty("title").GetString());
        Assert.Equal(before, FileCount());
    }

    [Fact]
    public async Task L2_030_2_Different_owners_can_use_the_same_key_without_disclosing_each_other()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var a = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var b = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var key = Guid.NewGuid().ToString();
        using var first = await SendAsync(a, key, ImageBytes());
        using var second = await SendAsync(b, key, ImageBytes());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var left = await first.Content.ReadFromJsonAsync<JsonElement>();
        var right = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(left.GetProperty("id").GetGuid(), right.GetProperty("id").GetGuid());
        using var hidden = await b.GetAsync(first.Headers.Location);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Theory]
    [InlineData(86399, false)]
    [InlineData(86400, true)]
    public async Task L2_030_5_Key_retention_expires_at_the_24_hour_boundary(int seconds, bool newOperation)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var key = Guid.NewGuid().ToString();
        var bytes = ImageBytes();
        using var first = await SendAsync(client, key, bytes);
        first.EnsureSuccessStatusCode();
        var saved = await first.Content.ReadFromJsonAsync<JsonElement>();
        factory.Clock.Advance(TimeSpan.FromSeconds(seconds));
        using var renewed = await factory.CreateAuthenticatedClientAsync(subject);
        using var retry = await SendAsync(renewed, key, bytes);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var replayed = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(newOperation, saved.GetProperty("id").GetGuid() != replayed.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task L2_030_5_Replaying_a_retained_key_for_a_missing_record_never_recreates_it()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var key = Guid.NewGuid().ToString();
        var bytes = ImageBytes();
        using var first = await SendAsync(client, key, bytes);
        first.EnsureSuccessStatusCode();
        var photo = await first.Content.ReadFromJsonAsync<JsonElement>();
        var id = photo.GetProperty("id").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Photographs.Where(item => item.Id == id).ExecuteDeleteAsync();
        var before = FileCount();
        using var retry = await SendAsync(client, key, bytes);
        Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Empty(list.GetProperty("items").EnumerateArray());
        Assert.Equal(before, FileCount());
    }

    [Theory]
    [InlineData(0, 400)]
    [InlineData(128, 201)]
    [InlineData(129, 400)]
    public async Task L2_030_1_Keys_are_required_and_bounded(int length, int status)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await SendAsync(client, length == 0 ? null : new string('k', length), ImageBytes());
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        if (status == 400)
        {
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(error.GetProperty("errors").TryGetProperty("operationKey", out _));
            var list = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
            Assert.Empty(list.GetProperty("items").EnumerateArray());
        }
    }

    private int FileCount() => Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0;

    private static byte[] ImageBytes(int width = 8)
    {
        using var image = Image.Black(width, 6, bands: 3);
        return image.PngsaveBuffer();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string? key, byte[] bytes, string title = "Still life", string? intent = null)
    {
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Study.png");
        upload.Add(new StringContent(title), "title");
        if (intent is not null) upload.Add(new StringContent(intent), "intent");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/photographs") { Content = upload };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
}
