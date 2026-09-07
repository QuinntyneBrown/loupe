namespace Loupe.Application.Images;

public sealed class ImageValidationException(ImageFailure failure) : Exception(failure switch
{
    ImageFailure.TooLarge => "Images must be 25 MB or smaller.",
    ImageFailure.Unsupported => "Choose a still JPEG, PNG, HEIC, or WebP image with its correct media type.",
    _ => "The image could not be decoded within the supported image limits."
})
{
    public ImageFailure Failure { get; } = failure;
}
