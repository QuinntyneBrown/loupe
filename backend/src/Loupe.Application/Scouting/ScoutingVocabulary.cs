using System.Reflection;
using System.Text.Json.Serialization;

namespace Loupe.Application.Scouting;

/// <summary>The shared labels of a scouting enumeration, as written in the specification and stored in report JSON.</summary>
public static class ScoutingVocabulary
{
    public static IReadOnlyList<string> Labels<T>() where T : struct, Enum => Enum.GetNames<T>()
        .Select(name => typeof(T).GetField(name)!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? name).ToArray();
}
