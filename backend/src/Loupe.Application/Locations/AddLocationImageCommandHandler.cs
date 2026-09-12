using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class AddLocationImageCommandHandler(ICurrentOwner owner, ILocationStore locations, ILocationImageStore locationImages,
    IImageIngestor ingestor, IImageStore images, IOperationReceiptStore receipts) : IRequestHandler<AddLocationImageCommand, LocationResult>
{
    public async Task<LocationResult> Handle(AddLocationImageCommand request, CancellationToken cancellationToken)
    {
        if (request.FileCount != 1) throw new RequestValidationException("image", "Choose exactly one image.");
        if (request.OperationKey is not { Length: > 0 and <= 128 } key || !key.All(character => character is >= '!' and <= '~'))
            throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        // Resolve ownership and capacity before any byte is read, so a foreign or full location retains nothing.
        _ = await locations.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        if (await locationImages.CountAsync(request.Id, owner.Id, cancellationToken) >= LocationImageLimit.Maximum)
            throw new RequestValidationException("images", LocationImageLimit.Message);
        var image = await request.Image.ReadAsync(cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.Id,
            image.Sha256,
            ContentType = request.Image.ContentType.ToLowerInvariant()
        })));
        await receipts.ExecuteAsync(owner.Id, "location-image", key, fingerprint, async token =>
        {
            var processed = ingestor.Process(image.Bytes, request.Image.ContentType, token);
            string? imageKey = null, previewKey = null;
            try
            {
                imageKey = await images.WriteAsync(processed.Image, token);
                previewKey = await images.WriteAsync(processed.Preview, token);
                await locationImages.AddAsync(request.Id, owner.Id, processed, imageKey, previewKey, token);
            }
            catch (Exception exception) when (exception is RequestValidationException or ResourceNotFoundException or ServiceUnavailableException)
            {
                // The store refused the add before commit; nothing references the staged objects.
                if (imageKey is not null) images.Delete(imageKey);
                if (previewKey is not null) images.Delete(previewKey);
                throw;
            }
            // Preserve staged files after an uncertain commit; cleanup verifies live references.
            return request.Id;
        }, cancellationToken);
        return LocationResult.From(await locations.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
