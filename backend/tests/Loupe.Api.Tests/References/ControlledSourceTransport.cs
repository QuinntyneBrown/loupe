namespace Loupe.Api.Tests.References;

public sealed class ControlledSourceTransport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        var response = await respond(request, cancellationToken);
        response.RequestMessage = request;
        return response;
    }
}
