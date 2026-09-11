using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Loupe.Application.Critiques;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class AzureOpenAiCritiqueProvider(IHttpClientFactory clients, IImageStore images, IOptions<AiOptions> options, TimeProvider clock) : ICritiqueProvider
{
    private static readonly JsonSerializerOptions InputJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
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

    public async Task<CritiqueResult> GenerateAsync(CritiqueInput input, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.Mode != ExecutionMode.Live) throw new ProviderFailureException(ProviderFailureKind.Disabled);
        if (!options.Value.IsConfigured) throw new ProviderFailureException(ProviderFailureKind.Disabled);
        if (input.PreviewKey is not { Length: 32 } || !input.PreviewKey.All(Uri.IsHexDigit))
            throw new ProviderFailureException(ProviderFailureKind.UnsupportedInput);
        try
        {
            using var image = images.OpenRead(input.PreviewKey);
            if (image.Length is <= 0 or > 8_000_000) throw new ProviderFailureException(ProviderFailureKind.UnsupportedInput);
            var bytes = new byte[(int)image.Length];
            await image.ReadExactlyAsync(bytes, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Value.Endpoint!), "openai/v1/responses"));
            request.Headers.Add("api-key", options.Value.ApiKey);
            request.Content = JsonContent.Create(new
            {
                model = options.Value.Deployment,
                store = false,
                max_output_tokens = 8000,
                instructions = CritiquePrompt.Instructions,
                input = new[] { new { role = "user", content = new object[] {
                    new { type = "input_text", text = JsonSerializer.Serialize(new { input.Brief, input.Exif }, InputJson) },
                    new { type = "input_image", image_url = "data:image/jpeg;base64," + Convert.ToBase64String(bytes), detail = "high" } } } },
                text = new { format = CritiqueResponseFormat.Create() }
            });
            using var client = clients.CreateClient("azure-openai");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) throw Failure(response);
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[2_000_001];
            var length = await content.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
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
            return result is null ? throw new ProviderFailureException(ProviderFailureKind.InvalidOutput)
                : JsonSerializer.Deserialize<CritiqueResult>(result, Json) ?? throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
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
