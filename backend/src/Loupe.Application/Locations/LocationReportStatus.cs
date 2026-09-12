using Loupe.Domain.Locations;
using Loupe.Domain.Operations;

namespace Loupe.Application.Locations;

public static class LocationReportStatus
{
    public static string Derive(Location location) => location.CurrentScoutingOperation?.Status switch
    {
        OperationStatus.Queued => "Queued",
        OperationStatus.Running => "Running",
        OperationStatus.Failed => "Failed",
        _ => location.ScoutingReportJson is null ? "None" : "Ready"
    };
}
