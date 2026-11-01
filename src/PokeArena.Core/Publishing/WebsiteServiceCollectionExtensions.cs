using Microsoft.Extensions.DependencyInjection;
using PokeArena.Core.Commands;

namespace PokeArena.Core.Publishing;

public static class WebsiteServiceCollectionExtensions
{
    /// <summary>
    /// Website publishing (spec §9): links for chat, the file generator, the R2 access and the upload worker. The
    /// host must provide an <see cref="Secrets.ISecretStore"/> and register <c>AddPokeArenaGame</c>.
    /// </summary>
    public static IServiceCollection AddPokeArenaWebsite(this IServiceCollection services, WebsiteOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<SiteLinks>();
        services.AddSingleton<SiteGenerator>();
        services.AddSingleton<R2Access>();
        services.AddSingleton<ISitePublisherFactory>(sp => sp.GetRequiredService<R2Access>());
        services.AddSingleton<SitePublishWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<SitePublishWorker>());
        services.AddSingleton<IChatCommand, ArenaCommand>();
        return services;
    }
}
