using Loupe.Application.Common;

namespace Loupe.Application.References;

public static class UploadReferenceCommandValidator
{
    public static string Key(UploadReferenceCommand request)
    {
        if (request.FileCount != 1) throw new RequestValidationException("image", "Choose exactly one image for each upload.");
        return request.OperationKey is { Length: > 0 and <= 128 } key && key.All(character => character is >= '!' and <= '~')
            ? key : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
    }
    public static ReferenceMetadata Normalize(UploadReferenceCommand request)
    {
        var title = TextField.Normalize(request.Title, 200, "title")
            ?? TextField.Default(Path.GetFileNameWithoutExtension(request.Image.Filename.Replace('\\', '/')), 200, "Untitled reference");
        return ReferenceMetadataValidator.Normalize(title, request.SourceUrl, request.Attribution, request.Notes);
    }
}
