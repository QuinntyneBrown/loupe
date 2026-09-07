using Loupe.Application.Common;

namespace Loupe.Application.Photographs;

public static class UploadPhotographCommandValidator
{
    public static void Files(UploadPhotographCommand request)
    {
        if (request.FileCount != 1) throw new RequestValidationException("image", "Choose exactly one image for each upload.");
    }
    public static string Key(UploadPhotographCommand request) =>
        request.OperationKey is { Length: > 0 and <= 128 } key && key.All(character => character is >= '!' and <= '~')
            ? key : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
    public static string Title(UploadPhotographCommand request) => TextField.Normalize(request.Title, 200, "title")
        ?? TextField.Default(Path.GetFileNameWithoutExtension(request.Image.Filename.Replace('\\', '/')), 200, "Untitled photograph");
}
