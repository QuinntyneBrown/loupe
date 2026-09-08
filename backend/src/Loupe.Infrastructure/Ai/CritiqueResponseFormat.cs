using System.Text.Json.Nodes;

namespace Loupe.Infrastructure.Ai;

public static class CritiqueResponseFormat
{
    public static JsonObject Create()
    {
        var properties = new List<(string, JsonNode)>
        {
            ("strengths", Array(Object(("explanation", Text()), ("evidence", Evidence()))))
        };
        foreach (var aspect in new[] { "exposure", "focus", "depthOfField", "motion", "lighting", "color", "processing",
            "framing", "subjectSeparation", "balance", "visualHierarchy", "mood" })
            properties.Add((aspect, Object(("assessable", new JsonObject { ["type"] = "boolean" }),
                ("explanation", Text()), ("uncertaintyReason", Text(true)), ("evidence", Evidence()))));
        properties.Add(("improvements", Array(Object(("observation", Text()), ("effect", Text()), ("action", Text()), ("evidence", Evidence())))));
        properties.Add(("exercise", Object(("action", Text()), ("comparison", Text()))));
        return new JsonObject { ["type"] = "json_schema", ["name"] = "loupe_critique", ["strict"] = true, ["schema"] = Object(properties.ToArray()) };
    }

    private static JsonObject Text(bool nullable = false) => new() { ["type"] = nullable ? new JsonArray("string", "null") : JsonValue.Create("string") };
    private static JsonObject Array(JsonNode item) => new() { ["type"] = "array", ["items"] = item };
    private static JsonObject Evidence() => Array(Object(
        ("kind", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("VisibleObservation", "ExifFact", "Hypothesis", "StylisticPreference") }),
        ("statement", Text()), ("exifField", Text(true)), ("exifValue", Text(true))));

    private static JsonObject Object(params (string Name, JsonNode Schema)[] fields)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, schema) in fields) { properties[name] = schema; required.Add(name); }
        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false };
    }
}
