using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Chat;
using PokeArena.Core.Commands;

namespace PokeArena.Core.Tests.Commands;

public class CommandTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly RecordingSender _chat = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("!pokearena", "pokearena", "")]
    [InlineData("  !PokeArena  ", "pokearena", "")]
    [InlineData("!fight @Misty extra", "fight", "@Misty extra")]
    [InlineData("!pokearena \U000E0000", "pokearena", "")]
    public void Parses_commands(string text, string name, string arguments)
    {
        Assert.Equal(new ParsedCommand(name, arguments), ParsedCommand.TryParse(text));
    }

    [Theory]
    [InlineData("hallo")]
    [InlineData("!")]
    [InlineData("! pokearena")]
    [InlineData("pokearena!")]
    public void Ignores_non_commands(string text)
    {
        Assert.Null(ParsedCommand.TryParse(text));
    }

    [Fact]
    public async Task Status_command_answers_with_the_version_then_cools_down()
    {
        var command = new StatusCommand(new AppInfo("0.1.0"), _time);

        await command.ExecuteAsync(Context("!pokearena"), _ct);
        await command.ExecuteAsync(Context("!pokearena"), _ct);
        _time.Advance(StatusCommand.Cooldown);
        await command.ExecuteAsync(Context("!pokearena"), _ct);

        Assert.Equal(["PokeArena-Bot ist online (Version 0.1.0).", "PokeArena-Bot ist online (Version 0.1.0)."], _chat.Sent);
    }

    [Fact]
    public async Task Status_command_skips_the_cooldown_in_the_dry_run()
    {
        var command = new StatusCommand(new AppInfo("0.1.0"), _time);

        await command.ExecuteAsync(Context("!pokearena", simulated: true), _ct);
        await command.ExecuteAsync(Context("!pokearena", simulated: true), _ct);

        Assert.Equal(2, _chat.Sent.Count);
    }

    [Fact]
    public async Task Processor_logs_every_message_and_runs_only_permitted_known_commands()
    {
        var modOnly = new RecordingCommand("geheim", CommandAccess.Moderators);
        var open = new RecordingCommand("offen", CommandAccess.Everyone);
        var chatLog = new ChatLog(_time);
        var processor = new CommandProcessor(new ChatInbox(), [modOnly, open], _chat, chatLog, NullLogger<CommandProcessor>.Instance);

        await processor.ProcessAsync(Message("!geheim"), _ct);
        await processor.ProcessAsync(Message("!geheim", ChatRole.Moderator), _ct);
        await processor.ProcessAsync(Message("!offen jetzt"), _ct);
        await processor.ProcessAsync(Message("!unbekannt"), _ct);
        await processor.ProcessAsync(Message("hallo"), _ct);

        Assert.Equal(1, modOnly.Calls);
        Assert.Equal(["jetzt"], open.Arguments);
        Assert.Equal(5, chatLog.Snapshot().Count(e => e.Kind == ChatLogKind.Incoming));
    }

    [Fact]
    public async Task Processor_survives_a_failing_command_and_notes_it_in_the_log()
    {
        var chatLog = new ChatLog(_time);
        var processor = new CommandProcessor(new ChatInbox(), [new FailingCommand()], _chat, chatLog, NullLogger<CommandProcessor>.Instance);

        await processor.ProcessAsync(Message("!kaputt"), _ct);

        Assert.Contains(chatLog.Snapshot(), e => e.Kind == ChatLogKind.Warning && e.Text.Contains("!kaputt"));
    }

    private CommandContext Context(string text, bool simulated = false) =>
        new(Message(text) with { IsSimulated = simulated }, "", _chat);

    private static ChatMessage Message(string text, ChatRole role = ChatRole.Viewer) =>
        new(Guid.NewGuid().ToString(), "1001", "ash", "Ash", text, role);

    private sealed class RecordingSender : IChatSender
    {
        public List<string> Sent { get; } = [];

        public void Send(string text) => Sent.Add(text);
    }

    private sealed class RecordingCommand(string name, CommandAccess access) : IChatCommand
    {
        public int Calls { get; private set; }

        public List<string> Arguments { get; } = [];

        public string Name => name;

        public CommandAccess Access => access;

        public Task ExecuteAsync(CommandContext context, CancellationToken ct)
        {
            Calls++;
            Arguments.Add(context.Arguments);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingCommand : IChatCommand
    {
        public string Name => "kaputt";

        public CommandAccess Access => CommandAccess.Everyone;

        public Task ExecuteAsync(CommandContext context, CancellationToken ct) => throw new InvalidOperationException("boom");
    }
}
