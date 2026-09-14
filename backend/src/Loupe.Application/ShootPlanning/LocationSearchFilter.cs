namespace Loupe.Application.ShootPlanning;

public sealed record LocationSearchFilter(string[] Tokens, string[] ShootTypes, int? People, string[] TimesOfDay, string? Setting, string[] Tags)
{
    /// <summary>A shoot-type, people, or time-of-day filter needs a report to evaluate, so report-less locations drop out.</summary>
    public bool RequiresReport => ShootTypes.Length > 0 || People is not null || TimesOfDay.Length > 0;
}
