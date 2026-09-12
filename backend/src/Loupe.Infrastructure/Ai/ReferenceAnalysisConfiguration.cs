using Loupe.Application.Common;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class ReferenceAnalysisConfiguration(IOptions<AiOptions> options) : IReferenceAnalysisConfiguration
{
    public AnalysisIdentity GetIdentity() => options.Value.IsConfigured
        ? new(ExecutionMode.Live, options.Value.Model, "reference-analysis-v1") : throw new IntegrationNotConfiguredException();
}
