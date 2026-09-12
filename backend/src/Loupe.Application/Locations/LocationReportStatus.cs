using Loupe.Domain.Locations;
using Loupe.Domain.Operations;
using Loupe.Domain.Scouting;

namespace Loupe.Application.Locations;

public static class LocationReportStatus
{
    public static string Derive(Location location, SavedScoutingReport? report) => Derive(location.CurrentScoutingOperation?.Status, report, location.ImageSetRevision);

    public static string Derive(OperationStatus? operation, SavedScoutingReport? report, long imageSetRevision) => operation switch
    {
        OperationStatus.Queued => "Queued",
        OperationStatus.Running => "Running",
        OperationStatus.Failed => "Failed",
        _ => report is null ? "None" : report.ImageSetRevision == imageSetRevision ? "Ready" : "Outdated"
    };
}
