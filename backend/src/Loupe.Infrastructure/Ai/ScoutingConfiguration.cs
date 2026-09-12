using Loupe.Application.Common;
using Loupe.Application.Scouting;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class ScoutingConfiguration(IOptions<AiOptions> options) : IScoutingConfiguration
{
    public AnalysisIdentity GetIdentity() => options.Value.IsConfigured
        ? new(ExecutionMode.Live, options.Value.Model, "location-scouting-v1") : throw new IntegrationNotConfiguredException();
}
