using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Operations;
using Loupe.Application.Videos;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class AzureOpenAiVideoEmbeddingProvider(IHttpClientFactory clients, IOptions<AiOptions> options, TimeProvider clock) : IVideoEmbeddingProvider
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (!options.Value.IsEmbeddingConfigured) throw new ProviderFailureException(ProviderFailureKind.Disabled);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Value.Endpoint!), "openai/v1/embeddings"));
            request.Headers.Add("api-key", options.Value.ApiKey);
            request.Content = JsonContent.Create(new { model = options.Value.EmbeddingDeployment, input = text, dimensions = VideoEmbedding.Dimensions });
            using var client = clients.CreateClient("azure-openai");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw Failure(response);
            await using var content = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new byte[2_000_001];
            var length = await content.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, timeout.Token);
            if (length > 2_000_000) throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
            using var document = JsonDocument.Parse(buffer.AsMemory(0, length), new JsonDocumentOptions { AllowDuplicateProperties = false });
            var data = document.RootElement.GetProperty("data");
            if (data.GetArrayLength() != 1) throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
            var vector = data[0].GetProperty("embedding").EnumerateArray().Select(value => value.GetSingle()).ToArray();
            if (vector.Length != VideoEmbedding.Dimensions || vector.Any(value => !float.IsFinite(value))) throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
            return vector;
        }
        catch (JsonException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
        catch (KeyNotFoundException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
        catch (InvalidOperationException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
        catch (FormatException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
        catch (HttpRequestException) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
        catch (IOException) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
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
