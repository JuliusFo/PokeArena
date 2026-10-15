using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PokeArena.Core.Data;

/// <summary>The game database — the single source of truth (ADR-0001). Short-lived: one instance per unit of work.</summary>
public sealed class PokeArenaDb(DbContextOptions<PokeArenaDb> options) : DbContext(options)
{
    public DbSet<Trainer> Trainers => Set<Trainer>();

    public DbSet<Specimen> Specimens => Set<Specimen>();

    public DbSet<Battle> Battles => Set<Battle>();

    public DbSet<BattleParticipant> BattleParticipants => Set<BattleParticipant>();

    public DbSet<AchievementUnlock> AchievementUnlocks => Set<AchievementUnlock>();

    public DbSet<Cooldown> Cooldowns => Set<Cooldown>();

    public static DbContextOptions<PokeArenaDb> Options(string connectionString) =>
        new DbContextOptionsBuilder<PokeArenaDb>().UseSqlite(connectionString).Options;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot compare or sort DateTimeOffset in queries; UTC ticks can.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Trainer>(trainer =>
        {
            trainer.HasKey(t => t.TwitchUserId);
            trainer.Property(t => t.TwitchUserId).HasMaxLength(32);
            trainer.Property(t => t.DisplayName).HasMaxLength(64);
            trainer.HasMany(t => t.Specimens).WithOne(s => s.Trainer).HasForeignKey(s => s.TrainerId);
        });

        modelBuilder.Entity<Specimen>(specimen =>
        {
            specimen.HasIndex(s => s.TrainerId, "IX_Specimens_TrainerId");
            specimen.HasIndex(s => s.TrainerId, "IX_Specimens_OneStarterPerTrainer")
                .IsUnique()
                .HasFilter("\"IsStarter\" = 1");
        });

        modelBuilder.Entity<Battle>(battle =>
        {
            battle.HasIndex(b => b.FoughtAt);
            battle.HasMany(b => b.Participants).WithOne().HasForeignKey(p => p.BattleId);
        });

        modelBuilder.Entity<BattleParticipant>(participant =>
        {
            participant.Property(p => p.TrainerId).HasMaxLength(32);
            participant.HasOne(p => p.Trainer).WithMany().HasForeignKey(p => p.TrainerId);
        });

        modelBuilder.Entity<AchievementUnlock>(unlock =>
        {
            unlock.HasKey(u => new { u.TrainerId, u.Threshold });
            unlock.Property(u => u.TrainerId).HasMaxLength(32);
            unlock.HasOne(u => u.Trainer).WithMany().HasForeignKey(u => u.TrainerId);
        });

        modelBuilder.Entity<Cooldown>(cooldown =>
        {
            cooldown.HasKey(c => c.Key);
            cooldown.Property(c => c.Key).HasMaxLength(64);
        });
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));
}

/// <summary>Lets <c>dotnet ef</c> build the model without the desktop host.</summary>
internal sealed class DesignTimeDbFactory : IDesignTimeDbContextFactory<PokeArenaDb>
{
    public PokeArenaDb CreateDbContext(string[] args) => new(PokeArenaDb.Options("Data Source=design-time.db"));
}
