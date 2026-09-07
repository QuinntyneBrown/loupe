using MediatR;

namespace Loupe.Application.Sessions;

public sealed class BeginSignInQueryHandler : IRequestHandler<BeginSignInQuery, string>
{
    public Task<string> Handle(BeginSignInQuery request, CancellationToken cancellationToken)
    {
        var returnUrl = string.IsNullOrWhiteSpace(request.ReturnUrl) ? "/my-work" : request.ReturnUrl.Trim();
        BeginSignInQueryValidator.Validate(returnUrl);
        return Task.FromResult(returnUrl);
    }
}
