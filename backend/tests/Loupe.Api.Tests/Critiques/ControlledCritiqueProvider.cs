using Loupe.Application.Critiques;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;

namespace Loupe.Api.Tests.Critiques;

public sealed class ControlledCritiqueProvider(Func<int, CritiqueInput, CancellationToken, Task<CritiqueResult>> generate) : ICritiqueProvider
{
    private int calls;
    public int Calls => calls;
    public Task<CritiqueResult> GenerateAsync(CritiqueInput input, AnalysisIdentity identity, CancellationToken cancellationToken) =>
        generate(Interlocked.Increment(ref calls), input, cancellationToken);
}
