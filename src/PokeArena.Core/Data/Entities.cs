namespace PokeArena.Core.Data;

/// <summary>A registered viewer. Identified only by the Twitch user id, never by name (spec §1).</summary>
public sealed class Trainer
{
    public required string TwitchUserId { get; init; }

    /// <summary>Current Twitch display name; refreshed when the trainer is seen in chat.</summary>
    public required string DisplayName { get; set; }

    public DateTimeOffset RegisteredAt { get; init; }

    public List<Specimen> Specimens { get; init; } = [];
}

public enum SpecimenOrigin
{
    Starter,
}

/// <summary>One owned Pokemon. Duplicates of a species are separate specimens (spec §1).</summary>
public sealed class Specimen
{
    public long Id { get; init; }

    public required string TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    /// <summary>Id in <see cref="SpeciesCatalog"/>; base values stay in the JSON (ADR-0001).</summary>
    public int SpeciesId { get; init; }

    public DateTimeOffset CaughtAt { get; init; }

    public SpecimenOrigin Origin { get; init; }

    /// <summary>The protected starter: exactly one per trainer, never transferred (spec §1, §6).</summary>
    public bool IsStarter { get; init; }
}
