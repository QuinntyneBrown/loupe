// Given a saved photograph or an older queued snapshot, when critique work runs,
// then the provider receives the immutable, metadata-stripped analysis preview.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Application.Images;
using Loupe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueAnalysisImageTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_006_1_041_1_Analysis_uses_a_bounded_private_preview_including_older_queued_work(bool olderSnapshot)
    {
        JsonElement submitted = default;
        var provider = new ControlledCritiqueProvider((_, input, _) =>
        {
            submitted = JsonSerializer.SerializeToElement(input);
            return Task.FromResult(CritiqueResultFixture.Valid());
        });
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, CritiqueProvider = provider };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var image = Image.Black(2400, 1200, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Wide.png");
        using var saved = await Photographs.PhotographFixture.SubmitAsync(client, upload);
        saved.EnsureSuccessStatusCode();
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var expected = await client.GetByteArrayAsync($"/api/photographs/{id}/preview");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admitted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        if (olderSnapshot)
        {
            await using var upgrade = factory.Services.CreateAsyncScope();
            var store = upgrade.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var migrator = store.GetService<IMigrator>();
            await migrator.MigrateAsync("20260908005942_AnalysisScheduling");
            await store.Database.ExecuteSqlInterpolatedAsync($"UPDATE background_operations SET \"InputJson\" = \"InputJson\" - 'PreviewKey' WHERE \"ResourceId\" = {id}");
            await migrator.MigrateAsync();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
        Assert.True(submitted.TryGetProperty("PreviewKey", out var preview), "The admitted provider input has no bounded analysis image.");
        Assert.NotEqual(submitted.GetProperty("ImageKey").GetString(), preview.GetString());
        using var stream = scope.ServiceProvider.GetRequiredService<IImageStore>().OpenRead(preview.GetString()!);
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        Assert.Equal(expected, bytes.ToArray());
        using var decoded = Image.NewFromBuffer(bytes.ToArray());
        Assert.Equal(1600, decoded.Width);
        Assert.Equal(800, decoded.Height);
        Assert.Equal(0, decoded.GetTypeOf("exif-data"));
        var status = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Succeeded", status.GetProperty("status").GetString());
    }
}
