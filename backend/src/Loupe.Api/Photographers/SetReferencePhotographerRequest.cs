namespace Loupe.Api.Photographers;

public sealed record SetReferencePhotographerRequest(long Revision, Guid? PhotographerId);
