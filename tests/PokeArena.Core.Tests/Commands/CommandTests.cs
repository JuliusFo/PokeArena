using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Chat;
using PokeArena.Core.Commands;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Commands;

public class CommandTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly GameFixture _game = new();
    private readonly ChatLog _chatLog = new(new FakeTimeProvider());
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
        var processor = Processor(modOnly, open);

        await processor.ProcessAsync(Message("!geheim"), _ct);
        await processor.ProcessAsync(Message("!geheim", ChatRole.Moderator), _ct);
        await processor.ProcessAsync(Message("!offen jetzt"), _ct);
        await processor.ProcessAsync(Message("!unbekannt"), _ct);
        await processor.ProcessAsync(Message("hallo"), _ct);

        Assert.Equal(1, modOnly.Calls);
        Assert.Equal(["jetzt"], open.Arguments);
        Assert.Equal(5, _chatLog.Snapshot().Count(e => e.Kind == ChatLogKind.Incoming));
    }

    [Fact]
    public async Task Processor_survives_a_failing_command_and_notes_it_in_the_log()
    {
        var processor = Processor(new FailingCommand());

        await processor.ProcessAsync(Message("!kaputt"), _ct);

        Assert.Contains(_chatLog.Snapshot(), e => e.Kind == ChatLogKind.Warning && e.Text.Contains("!kaputt"));
    }

    [Fact]
    public async Task Processor_survives_failing_queued_work_and_notes_it_in_the_log()
    {
        var processor = Processor();

        await processor.RunAsync(new QueuedWork("Boss-Kampf", _ => throw new InvalidOperationException("kaputt")), _ct);

        Assert.Contains(_chatLog.Snapshot(), e => e.Kind == ChatLogKind.Warning && e.Text.Contains("Boss-Kampf"));
    }

    [Fact]
    public async Task Processor_ignores_real_messages_during_the_dry_run_and_simulated_ones_outside_it()
    {
        var open = new RecordingCommand("offen", CommandAccess.Everyone);
        var processor = Processor(open);

        await processor.ProcessAsync(Message("!offen simuliert") with { IsSimulated = true }, _ct);
        _game.DryRun.Enabled = true;
        await processor.ProcessAsync(Message("!offen echt"), _ct);
        await processor.ProcessAsync(Message("!offen probe") with { IsSimulated = true }, _ct);

        Assert.Equal(["probe"], open.Arguments);
        Assert.Equal(3, _chatLog.Snapshot().Count(e => e.Kind == ChatLogKind.Incoming));
    }

    [Fact]
    public async Task Registered_only_commands_point_unregistered_users_to_the_registration()
    {
        var game = new RecordingCommand("spiel", CommandAccess.Registered);
        var processor = Processor(game, new RegisterCommand(_game.Trainers, GameFixture.Species));

        await processor.ProcessAsync(Message("!spiel"), _ct);
        await processor.ProcessAsync(Message("!registrieren pikachu"), _ct);
        await processor.ProcessAsync(Message("!spiel"), _ct);

        Assert.Equal(1, game.Calls);
        Assert.Equal(
            ["@Ash, du bist noch nicht dabei. Melde dich mit !registrieren <Starter> an.", "@Ash, willkommen bei PokeArena! Dein Starter ist Pikachu."],
            _chat.Sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mewtu")]
    public async Task Register_without_a_valid_starter_lists_the_choices(string arguments)
    {
        var command = new RegisterCommand(_game.Trainers, GameFixture.Species);

        await command.ExecuteAsync(new CommandContext(Message("!registrieren " + arguments), arguments, _chat), _ct);

        Assert.Equal(
            "@Ash, wähle deinen Starter: Bisasam, Schiggy, Glumanda, Pikachu oder Evoli – zum Beispiel: !registrieren Pikachu",
            Assert.Single(_chat.Sent));
        Assert.Empty(await _game.Trainers.ListAsync(_ct));
    }

    [Fact]
    public async Task Register_takes_the_first_word_and_reports_an_existing_registration()
    {
        var command = new RegisterCommand(_game.Trainers, GameFixture.Species);

        await command.ExecuteAsync(new CommandContext(Message("!registrieren"), "Evoli bitte", _chat), _ct);
        await command.ExecuteAsync(new CommandContext(Message("!registrieren"), "Glumanda", _chat), _ct);

        Assert.Equal(
            ["@Ash, willkommen bei PokeArena! Dein Starter ist Evoli.", "@Ash, du bist schon dabei – dein Starter ist Evoli."],
            _chat.Sent);
    }

    private CommandProcessor Processor(params IChatCommand[] commands) =>
        new(new ChatInbox(), commands, _chat, _game.Trainers, _game.DryRun, _chatLog, NullLogger<CommandProcessor>.Instance);

    public void Dispose() => _game.Dispose();

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
