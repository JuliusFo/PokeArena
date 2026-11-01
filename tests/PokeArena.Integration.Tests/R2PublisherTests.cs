using System.Text;
using Microsoft.Extensions.Configuration;
using PokeArena.Core.Publishing;
using PokeArena.Desktop.Services;

namespace PokeArena.Integration.Tests;

/// <summary>
/// Uploads and deletes a test file in the real R2 bucket. Skipped unless the bucket is configured (appsettings or the
/// git-ignored appsettings.local.json) and the R2 key is stored on this machine — always skipped in CI.
/// </summary>
public class R2PublisherTests
{
    [Fact]
    public async Task A_file_can_be_uploaded_and_deleted()
    {
        var options = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build()
            .GetSection(WebsiteOptions.SectionName)
            .Get<WebsiteOptions>() ?? new();
        using var access = new R2Access(options, new CredentialManagerSecretStore());
        var publisher = access.Create()!;
        Assert.SkipWhen(publisher is null, "No R2 bucket configured or no R2 key stored on this machine — enter it once in the desktop app.");

        var ct = TestContext.Current.CancellationToken;
        var path = $"integration-test/{Guid.NewGuid():N}.json";
        await publisher.PutAsync(path, new SiteFile(Encoding.UTF8.GetBytes("{\"test\":true}"), SiteGenerator.MutableCache), ct);
        await publisher.DeleteAsync(path, ct);

        // Deleting a file that is gone already succeeds too (S3 semantics), which the worker relies on.
        await publisher.DeleteAsync(path, ct);
    }
}
