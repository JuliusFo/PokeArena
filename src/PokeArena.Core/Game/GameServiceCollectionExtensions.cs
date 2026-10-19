using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Commands;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

public static class GameServiceCollectionExtensions
{
    /// <summary>
    /// Base data, database and game commands. Register before the chat services: the startup step here must finish
    /// (base data checked, database migrated) before the first command is processed. The host must provide
    /// <see cref="AppPaths"/> and <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="baseDataDirectory">Folder with pokemon.json, shipped next to the program.</param>
    public static IServiceCollection AddPokeArenaGame(this IServiceCollection services, string baseDataDirectory)
    {
        services.AddSingleton(_ => SpeciesCatalog.Load(Path.Combine(baseDataDirectory, "pokemon.json")));
        services.AddSingleton(sp => TypeChart.Load(Path.Combine(baseDataDirectory, "typen.json"), sp.GetRequiredService<SpeciesCatalog>()));
        services.AddSingleton(sp => NpcCatalog.Load(Path.Combine(baseDataDirectory, "npcs.json"), sp.GetRequiredService<SpeciesCatalog>()));
        services.AddSingleton<DryRunMode>();
        services.AddSingleton(sp => new GameDatabase(
            GameDatabase.FileConnectionString(sp.GetRequiredService<AppPaths>().DatabasePath),
            sp.GetRequiredService<DryRunMode>(),
            sp.GetRequiredService<ILogger<GameDatabase>>()));
        services.AddSingleton<TrainerService>();
        services.AddSingleton<BattleService>();

        // Balancing values: spec defaults for now; runtime editing comes with the config editor.
        services.AddSingleton(BattleSettings.Default);
        services.AddSingleton(BossSettings.Default);
        services.AddSingleton(PvpSettings.Default);
        services.AddSingleton<DamageCalculator>();
        services.AddSingleton<ISeedSource, SystemSeedSource>();
        services.AddSingleton<BossEncounter>();
        services.AddSingleton<PvpService>();

        services.AddSingleton<IChatCommand, RegisterCommand>();
        services.AddSingleton<IChatCommand, BossCommand>();
        services.AddSingleton<IChatCommand, FightCommand>();
        services.AddHostedService<GameStartup>();
        return services;
    }

    /// <summary>Fails the app start on broken base data or a failed migration instead of failing mid-game.</summary>
    private sealed class GameStartup(
        SpeciesCatalog species,
        TypeChart types,
        NpcCatalog npcs,
        GameDatabase database,
        AppPaths paths,
        ILogger<GameStartup> log) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            log.LogInformation("Base data: {Count} species, {Types} types, {Npcs} NPCs", species.All.Count, types.Types.Count, npcs.All.Count);
            Directory.CreateDirectory(paths.Root);
            await database.MigrateLiveAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
