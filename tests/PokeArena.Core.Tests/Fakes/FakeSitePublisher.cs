using System.Collections.Concurrent;
using PokeArena.Core.Publishing;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>An in-memory bucket. <see cref="Failure"/> makes every call throw; <see cref="OnPut"/> runs inside an upload.</summary>
public sealed class FakeSitePublisher : ISitePublisher, ISitePublisherFactory
{
    public ConcurrentDictionary<string, SiteFile> Files { get; } = new();

    public List<string> Calls { get; } = [];

    public Exception? Failure { get; set; }

    public bool IsSetUp { get; set; } = true;

    public Func<string, Task>? OnPut { get; set; }

    public event Action? Changed;

    public ISitePublisher? Create() => IsSetUp ? this : null;

    /// <summary>Like a new R2 token being saved.</summary>
    public void RaiseChanged() => Changed?.Invoke();

    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct)
    {
        Calls.Add("list " + prefix);
        if (Failure is { } failure)
        {
            throw failure;
        }

        return Task.FromResult<IReadOnlyList<string>>([.. Files.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)]);
    }

    public async Task PutAsync(string path, SiteFile file, CancellationToken ct)
    {
        Calls.Add("put " + path);
        if (Failure is { } failure)
        {
            throw failure;
        }

        if (OnPut is { } onPut)
        {
            await onPut(path);
        }

        Files[path] = file;
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        Calls.Add("delete " + path);
        if (Failure is { } failure)
        {
            throw failure;
        }

        Files.TryRemove(path, out _);
        return Task.CompletedTask;
    }
}
