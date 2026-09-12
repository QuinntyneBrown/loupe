using MediatR;

namespace Loupe.Application.ShootPlanning;

/// <summary>Claims one queued location index intent, embeds the current document, and publishes its vector; false when nothing was claimed.</summary>
public sealed record RefreshLocationSearchDocumentCommand : IRequest<bool>;
