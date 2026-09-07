using Loupe.Application.Common;

namespace Loupe.Application.Photographs;

public static class UploadPhotographCommandValidator
{
    public static string Title(UploadPhotographCommand request) => TextField.Normalize(request.Title, 200, "title")
        ?? TextField.Default(Path.GetFileNameWithoutExtension(request.Image.Filename.Replace('\\', '/')), 200, "Untitled photograph");
}
