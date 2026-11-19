using PokeArena.Core.Publishing;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>Links without a configured website: chat messages carry no link.</summary>
public static class NoSite
{
    public static SiteLinks Links { get; } = new(new WebsiteOptions());
}
