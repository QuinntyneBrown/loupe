using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.Scouting;
using Loupe.Domain.Operations;
using Loupe.Domain.Photographs;
using Loupe.Domain.Scouting;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class AzureOpenAiScoutingProvider(IHttpClientFactory clients, IImageStore images, IOptions<AiOptions> options, TimeProvider clock) : IScoutingProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        PropertyNameCaseInsensitive = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { type => { foreach (var property in type.Properties) property.IsRequired = true; } }
        },
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public async Task<ScoutingReport> GenerateAsync(ScoutingInput input, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.Mode != ExecutionMode.Live) throw new ProviderFailureException(ProviderFailureKind.Disabled);
        if (!options.Value.IsConfigured) throw new ProviderFailureException(ProviderFailureKind.Disabled);
        if (input.Images is not { Count: >= 1 and <= 10 } || input.Images.Any(image => image.PreviewKey is not { Length: 32 } || !image.PreviewKey.All(Uri.IsHexDigit)))
            throw new ProviderFailureException(ProviderFailureKind.UnsupportedInput);
        try
        {
            var content = new List<object> { new { type = "input_text", text = Brief(input) } };
            for (var number = 1; number <= input.Images.Count; number++)
            {
                var image = input.Images[number - 1];
                using var stream = images.OpenRead(image.PreviewKey);
                if (stream.Length is <= 0 or > 8_000_000) throw new ProviderFailureException(ProviderFailureKind.UnsupportedInput);
                var bytes = new byte[(int)stream.Length];
                await stream.ReadExactlyAsync(bytes, cancellationToken);
                content.Add(new { type = "input_text", text = Caption(image, number) });
                content.Add(new { type = "input_image", image_url = "data:image/jpeg;base64," + Convert.ToBase64String(bytes), detail = "high" });
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Value.Endpoint!), "openai/v1/responses"));
            request.Headers.Add("api-key", options.Value.ApiKey);
            request.Content = JsonContent.Create(new
            {
                model = options.Value.Deployment,
                store = false,
                max_output_tokens = 12000,
                instructions = ScoutingPrompt.Instructions,
                input = new[] { new { role = "user", content = content.ToArray() } },
                text = new { format = ScoutingResponseFormat.Create() }
            });
            using var client = clients.CreateClient("azure-openai");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) throw Failure(response);
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[2_000_001];
            var length = await body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
            if (length > 2_000_000) throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
            using var document = JsonDocument.Parse(buffer.AsMemory(0, length), new JsonDocumentOptions { AllowDuplicateProperties = false });
            var root = document.RootElement;
            if (root.GetProperty("status").GetString() != "completed") throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
            string? result = null;
            foreach (var output in root.GetProperty("output").EnumerateArray())
            {
                if (output.GetProperty("type").GetString() != "message") continue;
                foreach (var part in output.GetProperty("content").EnumerateArray())
                {
                    var type = part.GetProperty("type").GetString();
                    if (type == "refusal") throw new ProviderFailureException(ProviderFailureKind.UnsupportedInput);
                    if (type != "output_text") continue;
                    if (result is not null) throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
                    result = part.GetProperty("text").GetString();
                }
            }
            var parsed = result is null ? null : JsonSerializer.Deserialize<ScoutingOutput>(result, Json);
            return parsed?.ToReport(input) ?? throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
        }
        catch (JsonException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
        catch (KeyNotFoundException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
        catch (InvalidOperationException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
        catch (HttpRequestException) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
        catch (FileNotFoundException) { throw new ProviderFailureException(ProviderFailureKind.UnsupportedInput); }
        catch (IOException) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
        catch (UnauthorizedAccessException) { throw new ProviderFailureException(ProviderFailureKind.Disabled); }
    }

    private static string Brief(ScoutingInput input) => input.ScoutingBrief is { Length: > 0 } brief
        ? $"Photographer's brief (their own words, not verified): {brief}\n\nThe {input.Images.Count} images follow, numbered in order."
        : $"No brief was provided. The {input.Images.Count} images follow, numbered in order.";

    // Only the allowlisted capture settings travel with each image.
    private static string Caption(ScoutingImageInput image, int number)
    {
        var settings = new StringBuilder($"Image {number}");
        var exif = image.Exif ?? new CaptureMetadata();
        foreach (var (label, value) in new[] { ("camera", exif.Camera), ("lens", exif.Lens), ("aperture", exif.Aperture), ("shutter", exif.ShutterSpeed), ("ISO", exif.Iso), ("focal length", exif.FocalLength), ("captured", exif.CapturedAt) })
            if (!string.IsNullOrWhiteSpace(value)) settings.Append($" · {label} {value}");
        return settings.ToString();
    }

    private ProviderFailureException Failure(HttpResponseMessage response)
    {
        var kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ProviderFailureKind.InvalidCredentials,
            HttpStatusCode.Forbidden => ProviderFailureKind.AccessDenied,
            HttpStatusCode.NotFound => ProviderFailureKind.Disabled,
            HttpStatusCode.TooManyRequests => ProviderFailureKind.RateLimited,
            HttpStatusCode.RequestTimeout or HttpStatusCode.Conflict => ProviderFailureKind.Transient,
            _ => (int)response.StatusCode >= 500 ? ProviderFailureKind.Transient : ProviderFailureKind.UnsupportedInput
        };
        var retry = response.Headers.RetryAfter;
        return new ProviderFailureException(kind, retry?.Delta ?? (retry?.Date is { } date ? date - clock.GetUtcNow() : null));
    }
}
