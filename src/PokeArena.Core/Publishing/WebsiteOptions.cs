namespace PokeArena.Core.Publishing;

/// <summary>
/// Static website settings from the "PokeArena:Website" section. Nothing here is secret; the R2 API token lives
/// in the <see cref="Secrets.ISecretStore"/> (ADR-0002, ADR-0003).
/// </summary>
public sealed class WebsiteOptions
{
    public const string SectionName = "PokeArena:Website";

    /// <summary>Public address of the site, e.g. <c>https://arena.example.de</c>. Empty: no links in chat.</summary>
    public string SiteUrl { get; set; } = "";

    /// <summary>S3 endpoint of the Cloudflare account, <c>https://&lt;account-id&gt;.r2.cloudflarestorage.com</c>.</summary>
    public string R2Endpoint { get; set; } = "";

    public string Bucket { get; set; } = "";

    public bool HasSite => !string.IsNullOrWhiteSpace(SiteUrl);

    public bool HasBucket => !string.IsNullOrWhiteSpace(R2Endpoint) && !string.IsNullOrWhiteSpace(Bucket);
}
