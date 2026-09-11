using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Loupe.Application.PhotographerSummaries;

using Loupe.Application.Operations;
using Loupe.Domain.Photographers;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class AzureOpenAiPhotographerSummaryProvider(IHttpClientFactory clients, IOptions<AiOptions> options, TimeProvider clock) : IPhotographerSummaryProvider
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

    public async Task<PhotographerSummaryResult> GenerateAsync(CapturedPortfolioPage source, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.Mode != ExecutionMode.Live) throw new ProviderFailureException(ProviderFailureKind.Disabled);
        if (!options.Value.IsConfigured) throw new ProviderFailureException(ProviderFailureKind.Disabled);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Value.Endpoint!), "openai/v1/responses"));
            request.Headers.Add("api-key", options.Value.ApiKey);
            request.Content = JsonContent.Create(new
            {
                model = options.Value.Deployment,
                store = false,
                max_output_tokens = 8000,
                instructions = PhotographerSummaryPrompt.Instructions,
                input = new[] { new { role = "user", content = new[] { new { type = "input_text", text = JsonSerializer.Serialize(new { source.Title, source.Description, source.MainText }) } } } },
                text = new { format = PhotographerSummaryResponseFormat.Create() }
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
                : JsonSerializer.Deserialize<PhotographerSummaryResult>(result, Json) ?? throw new ProviderFailureException(ProviderFailureKind.InvalidOutput);
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
