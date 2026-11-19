using Microsoft.Extensions.DependencyInjection;
using PokeArena.Core.Commands;

namespace PokeArena.Core.Publishing;

public static class WebsiteServiceCollectionExtensions
{
    /// <summary>
    /// Website publishing (spec §9): links for chat, the file generator, the R2 access, the upload worker, the
    /// profile picture mirror and the re-render after an update (<see cref="RepublishAfterUpdate"/>). The host must
    /// provide an <see cref="Secrets.ISecretStore"/>, <see cref="AppInfo"/> and <see cref="AppPaths"/>, and register
    /// <c>AddPokeArenaGame</c> and <c>AddPokeArenaChat</c>.
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
        services.AddHostedService<ProfileImageRefresher>();
        services.AddHostedService<RepublishAfterUpdate>();
        services.AddSingleton<IChatCommand, ArenaCommand>();
        return services;
    }
}
