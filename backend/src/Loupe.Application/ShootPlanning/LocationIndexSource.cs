namespace Loupe.Application.ShootPlanning;

/// <summary>The document a claimed index intent embeds and the location revision it stands for.</summary>
public sealed record LocationIndexSource(long Revision, string Document);
