using System.Text.Json.Nodes;

namespace Loupe.Infrastructure.Ai;

public static class ScoutingResponseFormat
{
    // Every entry carries its evidence basis and the 1-based numbers of the images it rests on.
    private const string Evidence = """
        "basis":{"type":"string","enum":["Visible","Inferred"]},"citedImages":{"type":"array","items":{"type":"integer"}}
        """;

    private const string Schema = """
        {"type":"json_schema","name":"loupe_scouting_report","strict":true,"schema":{
          "type":"object","additionalProperties":false,
          "required":["overview","suitability","timesOfDay","techniques","groupSize","cautions"],
          "properties":{
            "overview":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["strength","reason","basis","citedImages"],"properties":{
              "strength":{"type":"string"},"reason":{"type":"string"},<EVIDENCE>}}},
            "suitability":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["shootType","rating","reason","basis","citedImages"],"properties":{
              "shootType":{"type":"string","enum":["Portraits","Family portraits","Headshots","Engagement","Events"]},
              "rating":{"type":"string","enum":["Well suited","Workable","Not recommended","Cannot assess"]},
              "reason":{"type":"string"},<EVIDENCE>}}},
            "timesOfDay":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["period","rating","reason","basis","citedImages"],"properties":{
              "period":{"type":"string","enum":["Dawn","Morning","Midday","Afternoon","Golden hour","Blue hour","Night"]},
              "rating":{"type":"string","enum":["Recommended","Avoid","Unknown"]},
              "reason":{"type":"string"},<EVIDENCE>}}},
            "techniques":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["technique","explanation","basis","citedImages"],"properties":{
              "technique":{"type":"string","enum":["Rule of thirds","Leading lines","Fill the frame","Colour theory","Near-far","Simplify the scene","Natural framing","Symmetry","Negative space","Layering","Patterns and repetition","Vantage point"]},
              "explanation":{"type":"string"},<EVIDENCE>}}},
            "groupSize":{"type":"object","additionalProperties":false,"required":["cannotAssess","minimum","maximum","reason","basis","citedImages"],"properties":{
              "cannotAssess":{"type":"boolean"},"minimum":{"type":["integer","null"]},"maximum":{"type":["integer","null"]},
              "reason":{"type":"string"},<EVIDENCE>}},
            "cautions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["caution","basis","citedImages"],"properties":{
              "caution":{"type":"string"},<EVIDENCE>}}}
          }
        }}
        """;

    public static JsonNode Create() => JsonNode.Parse(Schema.Replace("<EVIDENCE>", Evidence.Trim(), StringComparison.Ordinal))!;
}
