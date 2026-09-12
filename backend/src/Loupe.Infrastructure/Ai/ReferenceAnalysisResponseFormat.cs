using System.Text.Json.Nodes;

namespace Loupe.Infrastructure.Ai;

public static class ReferenceAnalysisResponseFormat
{
    public static JsonNode Create() => JsonNode.Parse("""
        {"type":"json_schema","name":"loupe_reference_analysis","strict":true,"schema":{
          "type":"object","additionalProperties":false,"required":["description","tags"],"properties":{
            "description":{"type":"string"},
            "tags":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["name","category"],"properties":{
              "name":{"type":"string"},"category":{"type":"string","enum":["subject","genre","composition","lighting","palette","mood","technique"]}
            }}}
          }
        }}
        """)!;
}
