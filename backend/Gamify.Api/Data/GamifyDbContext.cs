using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Data;

public sealed class GamifyDbContext(DbContextOptions<GamifyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(user => user.NormalizedEmail).IsUnique();
        modelBuilder.Entity<User>().Property(user => user.Name).HasMaxLength(80);
        modelBuilder.Entity<User>().Property(user => user.Email).HasMaxLength(254);
        modelBuilder.Entity<User>().Property(user => user.NormalizedEmail).HasMaxLength(254);
        modelBuilder.Entity<User>().HasMany(user => user.Platforms).WithMany().UsingEntity("UserPlatforms");

        modelBuilder.Entity<Platform>().HasData(
            new Platform { Id = Platform.PcId, Name = "PC" },
            new Platform { Id = Platform.PlayStationId, Name = "PlayStation" },
            new Platform { Id = Platform.XboxId, Name = "Xbox" },
            new Platform { Id = Platform.SwitchId, Name = "Switch" });

        modelBuilder.Entity<User>().HasMany(user => user.GroupMembers).WithOne()
            .HasForeignKey(member => member.UserId);

        GameCatalog.Seed(modelBuilder);

        modelBuilder.Entity<UserSession>().HasOne<User>().WithMany()
            .HasForeignKey(session => session.UserId);
        modelBuilder.Entity<UserSession>().HasIndex(session => session.ExpiresAt);
    }
}
