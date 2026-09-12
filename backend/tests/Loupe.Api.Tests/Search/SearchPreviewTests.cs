using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.References;
using Xunit;
using static Loupe.Api.Tests.Search.SearchFixture;

namespace Loupe.Api.Tests.Search;

public sealed class SearchPreviewTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_linked_images_and_links_when_searching_then_return_three_current_real_private_previews_and_the_full_count()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photographer = await Save(owner, "photographers", new { name = "Preview", portfolioUrl = "https://preview.example/" }, "photographer");
        var images = new List<JsonElement>();
        for (var index = 0; index < 5; index++)
        {
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
            using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["title"] = "Preview image " + index });
            upload.EnsureSuccessStatusCode();
            var image = await upload.Content.ReadFromJsonAsync<JsonElement>();
            images.Add(image);
            await Link(owner, image.GetProperty("id").GetGuid(), photographer);
        }
        var link = await Save(owner, "references/links", new { title = "Preview link", sourceUrl = "https://link.example/" }, "reference");
        await Link(owner, link, photographer);
        using var unlinked = await ReferenceFixture.SubmitAsync(owner);
        unlinked.EnsureSuccessStatusCode();
        var foreignPhotographer = await Save(stranger, "photographers", new { name = "Preview", portfolioUrl = "https://preview.example/" }, "photographer");
        using var foreign = await ReferenceFixture.SubmitAsync(stranger);
        foreign.EnsureSuccessStatusCode();
        await Link(stranger, (await foreign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), foreignPhotographer);

        var card = Assert.Single((await ReadSearch(owner, "type=photographers")).GetProperty("items").EnumerateArray());
        Assert.Equal(photographer, card.GetProperty("id").GetGuid());
        Assert.Equal(6, card.GetProperty("referenceCount").GetInt32());
        var previews = card.GetProperty("referencePreviewUrls").EnumerateArray().Select(url => url.GetString()!).ToArray();
        Assert.Equal(images.AsEnumerable().Reverse().Take(3).Select(image => PreviewPath(image.GetProperty("previewUrl").GetString()!)),
            previews.Select(PreviewPath));
        foreach (var url in previews)
        {
            using var fetched = await owner.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
            Assert.StartsWith("image/", fetched.Content.Headers.ContentType!.MediaType);
            using var decoded = NetVips.Image.NewFromBuffer(await fetched.Content.ReadAsByteArrayAsync());
            Assert.True(decoded.Width > 0 && decoded.Height > 0);
            using var denied = await stranger.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        var referenceCards = (await ReadSearch(owner, "query=preview&type=references")).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(6, referenceCards.Length);
        Assert.All(referenceCards.Where(item => item.GetProperty("id").GetGuid() != link), item =>
        {
            Assert.Equal(8, item.GetProperty("width").GetInt32());
            Assert.Equal(6, item.GetProperty("height").GetInt32());
            Assert.NotNull(item.GetProperty("previewUrl").GetString());
        });
        Assert.Equal(JsonValueKind.Null, referenceCards.Single(item => item.GetProperty("id").GetGuid() == link).GetProperty("previewUrl").ValueKind);

        var newest = images[^1].GetProperty("id").GetGuid();
        using var replaced = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["revision"] = "2" },
            path: $"/api/references/{newest}/image", method: HttpMethod.Put);
        replaced.EnsureSuccessStatusCode();
        var replacement = await replaced.Content.ReadFromJsonAsync<JsonElement>();
        var refreshed = Assert.Single((await ReadSearch(owner, "type=photographers")).GetProperty("items").EnumerateArray());
        Assert.NotEqual(previews[0], refreshed.GetProperty("referencePreviewUrls")[0].GetString());
        Assert.Equal(replacement.GetProperty("previewUrl").GetString(), refreshed.GetProperty("referencePreviewUrls")[0].GetString());
        using var deleted = await owner.DeleteAsync($"/api/references/{newest}?revision=3");
        deleted.EnsureSuccessStatusCode();
        var afterDeletion = Assert.Single((await ReadSearch(owner, "type=photographers")).GetProperty("items").EnumerateArray());
        Assert.Equal(5, afterDeletion.GetProperty("referenceCount").GetInt32());
        Assert.Equal(images.Take(4).Reverse().Take(3).Select(image => PreviewPath(image.GetProperty("previewUrl").GetString()!)),
            afterDeletion.GetProperty("referencePreviewUrls").EnumerateArray().Select(url => PreviewPath(url.GetString()!)));
    }

    private static string PreviewPath(string url) => new Uri(new Uri("https://localhost"), url).AbsolutePath;

    private static async Task Link(HttpClient owner, Guid reference, Guid photographer)
    {
        using var response = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { revision = 1, photographerId = photographer });
        response.EnsureSuccessStatusCode();
    }
}
