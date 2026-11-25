namespace Zanuraki.Mcp.LinkWatcher.Test.Services.Sources;

using System.Net;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = [];

    public List<HttpRequestMessage> Requests { get; } = [];

    public void RouteFixture(string url, string fixturePath, string? linkHeader = null) =>
        _routes[url] = () => Respond(HttpStatusCode.OK, Fixture.Read(fixturePath), linkHeader);

    public void RouteStatus(string url, HttpStatusCode statusCode) =>
        _routes[url] = () => new HttpResponseMessage(statusCode);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var route = _routes.GetValueOrDefault(request.RequestUri!.AbsoluteUri)
            ?? (() => new HttpResponseMessage(HttpStatusCode.NotFound));
        return Task.FromResult(route());
    }

    private static HttpResponseMessage Respond(HttpStatusCode statusCode, string body, string? linkHeader)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

        if (linkHeader is not null)
        {
            response.Headers.TryAddWithoutValidation("Link", linkHeader);
        }

        return response;
    }
}
