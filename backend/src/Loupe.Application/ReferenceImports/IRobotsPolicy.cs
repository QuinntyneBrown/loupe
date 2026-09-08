namespace Loupe.Application.ReferenceImports;

public interface IRobotsPolicy
{
    Task<RobotsDecision> EvaluateAsync(Uri target, CancellationToken cancellationToken);
}
