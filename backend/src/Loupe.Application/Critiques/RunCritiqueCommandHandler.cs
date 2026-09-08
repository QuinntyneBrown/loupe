using System.Text.Json;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed class RunCritiqueCommandHandler(ICritiqueWorkStore work, ICritiqueProvider provider) : IRequestHandler<RunCritiqueCommand, bool>
{
    public async Task<bool> Handle(RunCritiqueCommand request, CancellationToken cancellationToken)
    {
        var operation = await work.ClaimAsync(ExecutionMode.Demo, cancellationToken);
        if (operation is null) return false;
        var input = JsonSerializer.Deserialize<CritiqueInput>(operation.InputJson!)!;
        var result = await provider.GenerateAsync(input, new AnalysisIdentity(operation.Mode, operation.Model, operation.PromptVersion), cancellationToken);
        if (CritiqueResultValidator.IsValid(result, input.Exif)) await work.PublishAsync(operation, result, cancellationToken);
        else await work.RejectInvalidAsync(operation, cancellationToken);
        return true;
    }
}
