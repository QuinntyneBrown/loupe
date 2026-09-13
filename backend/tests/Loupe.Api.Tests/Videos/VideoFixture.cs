using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Videos;

public static class VideoFixture
{
    public static Task<HttpResponseMessage> Save(HttpClient owner, object input) => owner.PostAsJsonAsync("/api/videos", input);

    public static async Task<JsonElement> SaveOk(HttpClient owner, object input)
    {
        using var response = await Save(owner, input);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> List(HttpClient owner, string query = "")
    {
        using var response = await owner.GetAsync("/api/videos?" + query);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static Guid[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
}
