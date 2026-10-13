using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokeArena.Core.Chat;
using PokeArena.Core.Commands;
using PokeArena.Core.Twitch.Auth;
using PokeArena.Core.Twitch.EventSub;
using PokeArena.Core.Twitch.Helix;

namespace PokeArena.Core.Twitch;

public static class TwitchServiceCollectionExtensions
{
    /// <summary>
    /// Twitch connection, chat queue, sender and commands. The host must provide <see cref="TimeProvider"/>,
    /// <see cref="Secrets.ISecretStore"/> and <see cref="AppInfo"/>.
    /// </summary>
    public static IServiceCollection AddPokeArenaChat(this IServiceCollection services, TwitchOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
        {
            // A bot runs for hours: pick up DNS changes of the Twitch endpoints.
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
        });

        services.AddSingleton<TwitchAuthClient>();
        services.AddSingleton<TwitchTokenProvider>();
        services.AddSingleton<HelixClient>();
        services.AddSingleton<IEventSubSocketFactory, ClientWebSocketFactory>();
        services.AddSingleton<EventSubSession>();
        services.AddSingleton<TwitchConnection>();
        services.AddSingleton<ITwitchConnection>(sp => sp.GetRequiredService<TwitchConnection>());
        services.AddSingleton<IChatTargetSource>(sp => sp.GetRequiredService<TwitchConnection>());

        services.AddSingleton<ChatInbox>();
        services.AddSingleton<ChatLog>();
        services.AddSingleton<ChatSender>();
        services.AddSingleton<IChatSender>(sp => sp.GetRequiredService<ChatSender>());
        services.AddHostedService(sp => sp.GetRequiredService<ChatSender>());

        services.AddSingleton<IChatCommand, StatusCommand>();
        services.AddHostedService<CommandProcessor>();

        services.AddHostedService<TwitchStartup>();
        return services;
    }

    /// <summary>Connects on app start and disconnects cleanly on shutdown.</summary>
    private sealed class TwitchStartup(TwitchConnection connection) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            connection.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => connection.DisconnectAsync();
    }
}
