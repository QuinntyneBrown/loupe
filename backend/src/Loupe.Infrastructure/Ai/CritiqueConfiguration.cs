using Loupe.Application.Common;
using Loupe.Application.Critiques;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class CritiqueConfiguration(IOptions<AiOptions> options) : ICritiqueConfiguration
{
    public AnalysisIdentity GetIdentity() => options.Value.Mode switch
    {
        "Demo" => new(ExecutionMode.Demo, "loupe-demo-critique-v1", "critique-v1"),
        "Live" when !string.IsNullOrWhiteSpace(options.Value.ApiKey) => new(ExecutionMode.Live, options.Value.Model, "critique-v2"),
        _ => throw new IntegrationNotConfiguredException()
    };
}
