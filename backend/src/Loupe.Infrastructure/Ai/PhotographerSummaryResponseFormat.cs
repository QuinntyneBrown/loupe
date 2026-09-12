using System.Text.Json.Nodes;

namespace Loupe.Infrastructure.Ai;

public static class PhotographerSummaryResponseFormat
{
    public static JsonNode Create() => JsonNode.Parse("""
        {"type":"json_schema","name":"loupe_photographer_summary","strict":true,"schema":{
          "type":"object","additionalProperties":false,"required":["summary","tags","unavailableReason"],"properties":{
            "summary":{"type":["string","null"]},
            "unavailableReason":{"type":["string","null"],"enum":["insufficient_information",null]},
            "tags":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["name","category"],"properties":{
              "name":{"type":"string"},"category":{"type":"string","enum":["subject","genre","composition","lighting","palette","mood","technique"]}
            }}}
          }
        }}
        """)!;
}
