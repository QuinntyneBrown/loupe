using Loupe.Domain.Scouting;

namespace Loupe.Infrastructure.Ai;

public sealed record ScoutingOutputTimeOfDay(TimeOfDay Period, TimeOfDayRating Rating, string Reason, EvidenceBasis Basis, int[] CitedImages);
