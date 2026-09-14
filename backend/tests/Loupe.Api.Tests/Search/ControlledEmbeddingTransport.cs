using System.Net;
using System.Net.Http.Json;

namespace Loupe.Api.Tests.Search;

/// <summary>Stands in for the local Ollama endpoint: records every embedding request body and answers with hand-built vectors.</summary>
public sealed class ControlledEmbeddingTransport(Func<string, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public const int Dimensions = 1024;
    public List<(Uri Uri, string Body)> Requests { get; } = [];

    public static HttpResponseMessage Vector(int axis, string model = "bge-m3") => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { model, embeddings = new[] { Unit(axis) } })
    };

    public static float[] Unit(int axis)
    {
        var vector = new float[Dimensions];
        vector[axis] = 1f;
        return vector;
    }

    /// <summary>The requests that embedded one test's own location; the acceptance database is shared, so other intents may also flow through.</summary>
    public List<(Uri Uri, string Body)> RequestsFor(string marker) => Requests.Where(request => request.Body.Contains(marker)).ToList();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!, body));
        var response = await respond(body, cancellationToken);
        response.RequestMessage = request;
        return response;
    }
}
