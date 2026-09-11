using Loupe.Application.Common;
using Loupe.Application.Critiques;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class CritiqueConfiguration(IOptions<AiOptions> options) : ICritiqueConfiguration
{
    public AnalysisIdentity GetIdentity() => options.Value.Mode switch
    {
        "Live" when options.Value.IsConfigured => new(ExecutionMode.Live, options.Value.Model, "azure-critique-v2"),
        _ => throw new IntegrationNotConfiguredException()
    };
}
