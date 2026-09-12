using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Search;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

/// <summary>Embeds text through the Ollama instance running beside the API, so notes and briefs never leave the deployment.</summary>
public sealed class OllamaEmbeddingProvider(IHttpClientFactory clients, IOptions<EmbeddingOptions> options) : IEmbeddingProvider
{
    public const int Dimensions = 1024;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) throw new IntegrationNotConfiguredException();
        var client = clients.CreateClient("ollama");
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(new Uri(new Uri(settings.Endpoint!.TrimEnd('/') + "/"), "api/embed"),
                new { model = settings.Model, input = text }, cancellationToken);
        }
        catch (HttpRequestException) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ProviderFailureException(ProviderFailureKind.Transient); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new ProviderFailureException(response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => ProviderFailureKind.RateLimited,
                    HttpStatusCode.NotFound => ProviderFailureKind.Disabled,
                    HttpStatusCode.BadRequest => ProviderFailureKind.UnsupportedInput,
                    _ => ProviderFailureKind.Transient
                }, response.Headers.RetryAfter?.Delta);
            OllamaEmbedResponse? body;
            try { body = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken); }
            catch (JsonException) { throw new ProviderFailureException(ProviderFailureKind.InvalidOutput); }
            var vector = body?.Embeddings is [{ } single] ? single : null;
            if (vector is null || vector.Length != Dimensions || vector.Any(float.IsNaN) || vector.Any(float.IsInfinity))
                throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
            return vector;
        }
    }
}
