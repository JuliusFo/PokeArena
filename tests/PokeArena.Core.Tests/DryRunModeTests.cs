using PokeArena.Core.Chat;

namespace PokeArena.Core.Tests;

public class DryRunModeTests
{
    [Fact]
    public async Task The_switch_waits_for_its_turn_on_the_command_queue()
    {
        var inbox = new ChatInbox();
        var dryRun = new DryRunMode();

        dryRun.SwitchOnQueue(inbox, enabled: true);

        // Nothing changes until the command processor reaches the work, between two commands.
        Assert.False(dryRun.Enabled);
        Assert.True(inbox.Reader.TryRead(out var item));
        await Assert.IsType<QueuedWork>(item).RunAsync(TestContext.Current.CancellationToken);
        Assert.True(dryRun.Enabled);
    }
}
