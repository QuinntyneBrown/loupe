namespace Loupe.Application.ShootPlanning;

/// <summary>The embedded query and the model identity whose vectors it may be compared with.</summary>
public sealed record LocationSearchRanking(string Model, float[] Query)
{
    public const double Threshold = 0.20;
}
