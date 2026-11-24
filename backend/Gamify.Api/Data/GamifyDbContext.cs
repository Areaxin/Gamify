using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Data;

public sealed class GamifyDbContext(DbContextOptions<GamifyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<QueryTemplate> QueryTemplates => Set<QueryTemplate>();
    public DbSet<QueryHistoryEntry> QueryHistory => Set<QueryHistoryEntry>();
    public DbSet<StoreClick> StoreClicks => Set<StoreClick>();
    public DbSet<SavedPlaylist> SavedPlaylists => Set<SavedPlaylist>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(user => user.NormalizedEmail).IsUnique();
        modelBuilder.Entity<User>().Property(user => user.Name).HasMaxLength(80);
        modelBuilder.Entity<User>().Property(user => user.Email).HasMaxLength(254);
        modelBuilder.Entity<User>().Property(user => user.NormalizedEmail).HasMaxLength(254);
        modelBuilder.Entity<User>().Property(user => user.Role).HasMaxLength(32).HasDefaultValue("User");
        modelBuilder.Entity<User>().HasMany(user => user.Platforms).WithMany().UsingEntity("UserPlatforms");

        modelBuilder.Entity<Platform>().HasData(
            new Platform { Id = Platform.PcId, Name = "PC" },
            new Platform { Id = Platform.PlayStationId, Name = "PlayStation" },
            new Platform { Id = Platform.XboxId, Name = "Xbox" },
            new Platform { Id = Platform.SwitchId, Name = "Switch" });

        modelBuilder.Entity<User>().HasMany(user => user.GroupMembers).WithOne()
            .HasForeignKey(member => member.UserId);
        modelBuilder.Entity<User>().HasMany(user => user.SavedPlaylists).WithOne()
            .HasForeignKey(playlist => playlist.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SavedPlaylist>().Property(playlist => playlist.Name).HasMaxLength(100);

        modelBuilder.Entity<UserSession>().HasOne<User>().WithMany()
            .HasForeignKey(session => session.UserId);
        modelBuilder.Entity<UserSession>().HasIndex(session => session.ExpiresAt);

        modelBuilder.Entity<QueryHistoryEntry>().HasOne<User>().WithMany()
            .HasForeignKey(entry => entry.UserId);
        modelBuilder.Entity<QueryHistoryEntry>().Property(entry => entry.Text).HasMaxLength(1000);
        modelBuilder.Entity<QueryHistoryEntry>().HasIndex(entry => new { entry.UserId, entry.CreatedAt, entry.Id });
        modelBuilder.Entity<QueryHistoryEntry>().HasIndex(entry => new { entry.UserId, entry.Text, entry.ForGroup }).IsUnique();

        modelBuilder.Entity<StoreClick>().HasOne<User>().WithMany().HasForeignKey(click => click.UserId);
        modelBuilder.Entity<StoreClick>().Property(click => click.Store).HasMaxLength(40);
        modelBuilder.Entity<StoreClick>().Property(click => click.StoreUrl).HasMaxLength(500);
        modelBuilder.Entity<StoreClick>().HasIndex(click => new { click.StoreUrl, click.ClickedAt });

        modelBuilder.Entity<AdminAuditLog>().HasIndex(entry => entry.OccurredAt);
        
modelBuilder.Entity<QueryTemplate>().HasData(
    new QueryTemplate
    {
        Id = 1,
        Title = "Ramus žaidimas vienam",
        Text = "Ieškau ramaus žaidimo vienam, iki 15 eurų, pradedantiesiems.",
        SortOrder = 1,
        IsActive = true
    },
    new QueryTemplate
    {
        Id = 2,
        Title = "Žaidimas dviem draugams",
        Text = "Ieškau smagaus žaidimo dviem žmonėms, kurį galėčiau žaisti su draugu.",
        SortOrder = 2,
        IsActive = true
    },
    new QueryTemplate
    {
        Id = 3,
        Title = "Siaubo žaidimas",
        Text = "Ieškau siaubo žaidimo su gera istorija ir įtempta atmosfera.",
        SortOrder = 3,
        IsActive = true
    },
    new QueryTemplate
    {
        Id = 4,
        Title = "Žaidimas keturiems draugams",
        Text = "Ieškau smagaus žaidimo keturiems draugams, iki 20 eurų.",
        SortOrder = 4,
        IsActive = true
    },
    new QueryTemplate
    {
        Id = 5,
        Title = "Žaidimas pradedantiesiems",
        Text = "Ieškau lengvai išmokstamo žaidimo pradedantiesiems.",
        SortOrder = 5,
        IsActive = true
    });

    }
}
