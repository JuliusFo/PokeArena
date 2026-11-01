using System.Net;
using System.Text;

namespace PokeArena.Web.Tests;

/// <summary>The data host as a dictionary of JSON files; missing paths answer 404, <see cref="Down"/> fails every request.</summary>
public sealed class FakeDataHost : HttpMessageHandler
{
    public Dictionary<string, string> Files { get; } = [];

    public bool Down { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Down)
        {
            throw new HttpRequestException("offline");
        }

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        return Task.FromResult(Files.TryGetValue(path, out var json)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
