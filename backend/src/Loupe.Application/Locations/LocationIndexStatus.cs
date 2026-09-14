using Loupe.Domain.Locations;
using Loupe.Domain.Operations;

namespace Loupe.Application.Locations;

/// <summary>How the location's search vector stands: waiting on a report, updating, failed, current, or not configured at all.</summary>
public static class LocationIndexStatus
{
    public static string Derive(Location location, bool configured) =>
        Derive(location.CurrentScoutingOperation?.Status, location.CurrentIndexOperation?.Status, configured);

    public static string Derive(OperationStatus? scouting, OperationStatus? index, bool configured)
    {
        if (!configured) return "not-configured";
        if (scouting is OperationStatus.Queued or OperationStatus.Running) return "processing-report";
        return index switch
        {
            OperationStatus.Succeeded => "current",
            OperationStatus.Failed => "failed",
            _ => "updating"
        };
    }
}
