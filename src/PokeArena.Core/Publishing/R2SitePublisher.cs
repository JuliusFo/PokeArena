using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using PokeArena.Core.Secrets;

namespace PokeArena.Core.Publishing;

/// <summary>Uploads site files to the Cloudflare R2 bucket through its S3 API (ADR-0003).</summary>
public sealed class R2SitePublisher : ISitePublisher, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public R2SitePublisher(string endpoint, string bucket, string accessKeyId, string secretAccessKey)
    {
        _bucket = bucket;
        _client = new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,

            // R2 rejects the SDK's default checksum headers and streaming payload signatures.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
    }

    public async Task PutAsync(string path, SiteFile file, CancellationToken ct)
    {
        using var content = new MemoryStream(file.Content, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = path,
            InputStream = content,
            ContentType = "application/json; charset=utf-8",
            DisablePayloadSigning = true,
        };
        request.Headers.CacheControl = file.CacheControl;
        await RunAsync(() => _client.PutObjectAsync(request, ct));
    }

    public Task DeleteAsync(string path, CancellationToken ct) =>
        RunAsync(() => _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = path }, ct));

    public async Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct)
    {
        var paths = new List<string>();
        var request = new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix };
        ListObjectsV2Response response;
        do
        {
            response = await RunAsync(() => _client.ListObjectsV2Async(request, ct));
            paths.AddRange((response.S3Objects ?? []).Select(o => o.Key));
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true);

        return paths;
    }

    public void Dispose() => _client.Dispose();

    private static async Task<T> RunAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new SitePublishException("Cloudflare lehnt die Zugangsdaten ab. Bitte die R2-Schlüssel prüfen.", ex);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SitePublishException("Den Bucket gibt es bei Cloudflare nicht. Bitte den Bucket-Namen prüfen.", ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new SitePublishException($"Cloudflare meldet einen Fehler ({(int)ex.StatusCode}).", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or AmazonServiceException or IOException)
        {
            throw new SitePublishException("Keine Verbindung zu Cloudflare. Ist der PC online?", ex);
        }
    }
}

/// <summary>
/// The R2 access for the configured bucket: stores the API token in the secret store and hands out one shared
/// publisher until the token changes.
/// </summary>
public sealed class R2Access(WebsiteOptions options, ISecretStore secrets) : ISitePublisherFactory, IDisposable
{
    public const string AccessKeyIdSecret = "r2-access-key-id";
    public const string SecretAccessKeySecret = "r2-secret-access-key";

    private readonly Lock _lock = new();
    private R2SitePublisher? _publisher;

    /// <summary>
    /// Publishers replaced by a new token. The publish worker may still be uploading with one, so they are disposed
    /// only with this object (review 1.0.3, A26).
    /// </summary>
    private readonly List<R2SitePublisher> _retired = [];

    /// <summary>Raised after the token was saved or removed.</summary>
    public event Action? Changed;

    public WebsiteOptions Options => options;

    public bool HasCredentials => secrets.Get(AccessKeyIdSecret) is { Length: > 0 } && secrets.Get(SecretAccessKeySecret) is { Length: > 0 };

    public void SaveCredentials(string accessKeyId, string secretAccessKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretAccessKey);
        secrets.Set(AccessKeyIdSecret, accessKeyId.Trim());
        secrets.Set(SecretAccessKeySecret, secretAccessKey.Trim());
        Reset();
    }

    public void DeleteCredentials()
    {
        secrets.Delete(AccessKeyIdSecret);
        secrets.Delete(SecretAccessKeySecret);
        Reset();
    }

    public ISitePublisher? Create()
    {
        lock (_lock)
        {
            if (_publisher is null && options.HasBucket
                && secrets.Get(AccessKeyIdSecret) is { Length: > 0 } keyId
                && secrets.Get(SecretAccessKeySecret) is { Length: > 0 } secret)
            {
                _publisher = new R2SitePublisher(options.R2Endpoint, options.Bucket, keyId, secret);
            }

            return _publisher;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _publisher?.Dispose();
            _publisher = null;
            _retired.ForEach(p => p.Dispose());
            _retired.Clear();
        }
    }

    private void Reset()
    {
        lock (_lock)
        {
            if (_publisher is not null)
            {
                _retired.Add(_publisher);
                _publisher = null;
            }
        }

        Changed?.Invoke();
    }
}
