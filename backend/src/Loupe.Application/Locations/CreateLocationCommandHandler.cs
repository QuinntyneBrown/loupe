using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using Loupe.Domain.Locations;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class CreateLocationCommandHandler(ICurrentOwner owner, ILocationStore locations, IOperationReceiptStore receipts, TimeProvider clock)
    : IRequestHandler<CreateLocationCommand, LocationResult>
{
    public async Task<LocationResult> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var key = request.OperationKey is { Length: > 0 and <= 128 } value && value.All(character => character is >= '!' and <= '~')
            ? value : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        var details = LocationDetailsValidator.Normalize(request);
        if (request.Tags?.Length > 50 || request.Tags?.Any(tag => tag is null) == true) throw new RequestValidationException("tags", "Supply up to 50 tags.");
        var tags = (request.Tags ?? []).Select(tag => new LocationTagInput(TagName.Validate(tag.Name), TagName.Category(tag.Category)))
            .DistinctBy(tag => tag.Name!.ToUpperInvariant()).ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { details, tags })));
        var id = await receipts.ExecuteAsync(owner.Id, "location", key, fingerprint, async token =>
        {
            var now = clock.GetUtcNow();
            var location = new Location
            {
                Id = Guid.NewGuid(),
                OwnerId = owner.Id,
                Name = details.Name,
                AddressLine1 = details.AddressLine1,
                AddressLine2 = details.AddressLine2,
                Locality = details.Locality,
                Region = details.Region,
                PostalCode = details.PostalCode,
                Country = details.Country,
                ScoutingBrief = details.ScoutingBrief,
                Notes = details.Notes,
                CreatedAt = now,
                UpdatedAt = now
            };
            foreach (var tag in tags)
                location.Tags.Add(new LocationTag { LocationId = location.Id, OwnerId = owner.Id, NormalizedName = tag.Name!.ToUpperInvariant(), Name = tag.Name!, Category = tag.Category });
            await locations.SaveAsync(location, token);
            return location.Id;
        }, cancellationToken);
        return LocationResult.From(await locations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
