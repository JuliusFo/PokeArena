using System.Net;
using System.Text;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>Answers HTTP requests from queued responses per "METHOD url-prefix" and records every request.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Lock _lock = new();
    private readonly List<(string Method, string UrlPrefix, Queue<Func<HttpResponseMessage>> Responses)> _routes = [];

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Queues a response; the last one queued for a route repeats once the queue is down to it.</summary>
    public FakeHttpHandler On(HttpMethod method, string urlPrefix, HttpStatusCode status, string json = "{}")
    {
        lock (_lock)
        {
            var route = _routes.FirstOrDefault(r => r.Method == method.Method && r.UrlPrefix == urlPrefix);
            if (route.Responses is null)
            {
                route = (method.Method, urlPrefix, new Queue<Func<HttpResponseMessage>>());
                _routes.Add(route);
            }

            route.Responses.Enqueue(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }

        return this;
    }

    public IEnumerable<RecordedRequest> RequestsTo(string urlPrefix)
    {
        lock (_lock)
        {
            return Requests.Where(r => r.Url.StartsWith(urlPrefix, StringComparison.Ordinal)).ToList();
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var url = request.RequestUri!.ToString();
        lock (_lock)
        {
            Requests.Add(new RecordedRequest(
                request.Method.Method,
                url,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Client-Id", out var ids) ? ids.Single() : null,
                body));

            var route = _routes
                .Where(r => r.Method == request.Method.Method && url.StartsWith(r.UrlPrefix, StringComparison.Ordinal))
                .OrderByDescending(r => r.UrlPrefix.Length)
                .FirstOrDefault();
            if (route.Responses is null || route.Responses.Count == 0)
            {
                throw new InvalidOperationException($"No fake response for {request.Method} {url}");
            }

            return route.Responses.Count > 1 ? route.Responses.Dequeue()() : route.Responses.Peek()();
        }
    }
}

public sealed record RecordedRequest(string Method, string Url, string? Authorization, string? ClientId, string Body);
