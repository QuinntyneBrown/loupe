using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Loupe.Api.Tests.Scouting;

public static class ScoutingReportFixture
{
    public static readonly string[] ShootTypes = ["Portraits", "Family portraits", "Headshots", "Engagement", "Events"];
    public static readonly string[] Periods = ["Dawn", "Morning", "Midday", "Afternoon", "Golden hour", "Blue hour", "Night"];

    /// <summary>A complete, valid provider result citing the given image numbers (1-based) on every entry.</summary>
    public static JsonObject Valid(params int[] images)
    {
        var cited = images.Length == 0 ? [1] : images;
        JsonArray Cited() => new(cited.Select(number => JsonValue.Create(number)).ToArray<JsonNode?>());
        return new JsonObject
        {
            ["overview"] = new JsonArray(
                Entry(new JsonObject { ["strength"] = "Even open shade along the foreshore", ["reason"] = "The trees on the north bank hold soft shade in every frame." }, "Visible", Cited()),
                Entry(new JsonObject { ["strength"] = "Long uninterrupted sightlines", ["reason"] = "The bridge arches recede across three images without clutter." }, "Visible", Cited())),
            ["suitability"] = new JsonArray(ShootTypes.Select(type => Entry(new JsonObject
            {
                ["shootType"] = type,
                ["rating"] = type == "Events" ? "Not recommended" : "Well suited",
                ["reason"] = type == "Events" ? "The gravel strip is narrow and the tide reaches it." : "Open shade and clean backgrounds suit a small group."
            }, "Visible", Cited())).ToArray<JsonNode?>()),
            ["timesOfDay"] = new JsonArray(Periods.Select(period => Entry(new JsonObject
            {
                ["period"] = period,
                ["rating"] = period == "Golden hour" ? "Recommended" : period == "Midday" ? "Avoid" : "Unknown",
                ["reason"] = period == "Golden hour" ? "Open sky to the west lights the arches from the side." : period == "Midday" ? "Bare gravel and water reflect hard overhead light." : "The images give no basis for this period."
            }, period is "Golden hour" or "Midday" ? "Visible" : "Inferred", Cited())).ToArray<JsonNode?>()),
            ["techniques"] = new JsonArray(
                Entry(new JsonObject { ["technique"] = "Leading lines", ["explanation"] = "Use the arches and the water's edge to lead into the subject." }, "Visible", Cited()),
                Entry(new JsonObject { ["technique"] = "Natural framing", ["explanation"] = "Frame a couple inside the nearest arch from the gravel." }, "Visible", Cited())),
            ["groupSize"] = Entry(new JsonObject { ["cannotAssess"] = false, ["minimum"] = 1, ["maximum"] = 6, ["reason"] = "The dry gravel strip fits a small group with room to move." }, "Visible", Cited()),
            ["cautions"] = new JsonArray(
                Entry(new JsonObject { ["caution"] = "Wet stones near the waterline are slippery." }, "Visible", Cited()))
        };
    }

    private static JsonObject Entry(JsonObject fields, string basis, JsonArray cited)
    {
        fields["basis"] = basis;
        fields["citedImages"] = cited;
        return fields;
    }

    public static HttpResponseMessage Output(JsonNode report) => Output(report.ToJsonString());

    public static HttpResponseMessage Output(string text) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } })
    };

    public static HttpResponseMessage Refusal() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "refusal", refusal = "I cannot help with that." } } } } })
    };

    public static async Task<HttpResponseMessage> RequestAsync(HttpClient client, Guid id, long revision, bool regenerate = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/locations/{id}/scouting-report") { Content = JsonContent.Create(new { revision, regenerate }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public static async Task<JsonElement> WaitForAsync(HttpClient client, Uri? location, Func<JsonElement, bool> done, int attempts = 100)
    {
        JsonElement operation = default;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            operation = await client.GetFromJsonAsync<JsonElement>(location);
            if (done(operation)) return operation;
            await Task.Delay(100);
        }
        return operation;
    }
}
