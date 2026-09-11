using Loupe.Application.Common;
using Loupe.Application.PhotographerSummaries;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class PhotographerSummaryConfiguration(IOptions<AiOptions> options) : IPhotographerSummaryConfiguration
{
    public AnalysisIdentity GetIdentity() => options.Value.IsConfigured
        ? new(ExecutionMode.Live, options.Value.Model, "photographer-summary-v1") : throw new IntegrationNotConfiguredException();
}
