using System.Text.Json.Serialization;

namespace Loupe.Domain.Scouting;

public enum CompositionTechnique
{
    [JsonStringEnumMemberName("Rule of thirds")] RuleOfThirds,
    [JsonStringEnumMemberName("Leading lines")] LeadingLines,
    [JsonStringEnumMemberName("Fill the frame")] FillTheFrame,
    [JsonStringEnumMemberName("Colour theory")] ColourTheory,
    [JsonStringEnumMemberName("Near-far")] NearFar,
    [JsonStringEnumMemberName("Simplify the scene")] SimplifyTheScene,
    [JsonStringEnumMemberName("Natural framing")] NaturalFraming,
    [JsonStringEnumMemberName("Symmetry")] Symmetry,
    [JsonStringEnumMemberName("Negative space")] NegativeSpace,
    [JsonStringEnumMemberName("Layering")] Layering,
    [JsonStringEnumMemberName("Patterns and repetition")] PatternsAndRepetition,
    [JsonStringEnumMemberName("Vantage point")] VantagePoint,
}
