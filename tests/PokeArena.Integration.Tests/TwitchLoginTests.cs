using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Core.Twitch;
using PokeArena.Core.Twitch.Auth;
using PokeArena.Core.Twitch.Helix;
using PokeArena.Desktop.Services;

namespace PokeArena.Integration.Tests;

/// <summary>
/// Checks the bot login stored on this machine against the real Twitch API. Skipped when no login is stored
/// (always in CI). Reads only — but a due refresh rotates the stored token, so don't run it while the app is
/// refreshing at the same moment.
/// </summary>
public class TwitchLoginTests
{
    [Fact]
    public async Task Stored_login_is_valid_and_resolves_the_bot_account()
    {
        var secrets = new CredentialManagerSecretStore();
        if (secrets.Get(TwitchTokenProvider.SecretName) is null)
        {
            Assert.Skip("No Twitch login stored on this machine — sign in once in the desktop app.");
        }

        var ct = TestContext.Current.CancellationToken;
        var options = new TwitchOptions { ClientId = ReadClientId(), Channel = "-" };
        using var http = new HttpClient();
        var tokens = new TwitchTokenProvider(
            new TwitchAuthClient(http, options, TimeProvider.System),
            secrets,
            TimeProvider.System,
            NullLogger<TwitchTokenProvider>.Instance);

        var bot = await tokens.ValidateAsync(ct);
        var user = await new HelixClient(http, options, tokens).GetUserByLoginAsync(bot.Login, ct);

        Assert.Equal(bot.UserId, user?.Id);
    }

    private static string ReadClientId()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        return json.RootElement.GetProperty("PokeArena").GetProperty("Twitch").GetProperty("ClientId").GetString()!;
    }
}
