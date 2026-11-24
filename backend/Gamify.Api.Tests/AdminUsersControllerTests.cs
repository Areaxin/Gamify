using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Gamify.Api.Controllers;
using Gamify.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gamify.Api.Tests;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task List_ReturnsOnlyMinimalUserFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GamifyDbContext>().UseSqlite(connection).Options;
        await using var db = new GamifyDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var registeredAt = DateTimeOffset.UtcNow.AddDays(-1);
        db.Users.Add(User(101, "Player", "player@gmail.com", registeredAt));
        await db.SaveChangesAsync();

        var result = await Controller(db).List(CancellationToken.None);

        var users = Assert.IsType<OkObjectResult>(result).Value as List<AdminUserResponse>;
        var listedUser = Assert.Single(users!);
        Assert.Equal(101, listedUser.Id);
        Assert.Equal("Player", listedUser.Username);
        Assert.Equal("player@gmail.com", listedUser.Email);
        Assert.Equal(registeredAt, listedUser.RegisteredAt);
        Assert.Equal(["Id", "Username", "Email", "RegisteredAt"],
            typeof(AdminUserResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task Delete_RemovesAccountDataAndWritesPiiFreeAuditRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GamifyDbContext>().UseSqlite(connection).Options;
        await using var db = new GamifyDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(
            User(101, "Admin", "admin@gmail.com"),
            User(202, "Player", "player@gmail.com"));
        db.UserSessions.Add(new UserSession { Id = Guid.NewGuid(), UserId = 202, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        db.Set<GroupMember>().Add(new GroupMember { UserId = 202, BirthDate = new DateOnly(2000, 1, 1) });
        db.QueryHistory.Add(new QueryHistoryEntry { UserId = 202, Text = "private query", CreatedAt = DateTime.UtcNow });
        db.SavedPlaylists.Add(new SavedPlaylist { UserId = 202, Name = "Private playlist" });
        db.StoreClicks.Add(new StoreClick { UserId = 202, Store = "store", ClickedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await Controller(db).Delete(202, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await db.Users.AnyAsync(user => user.Id == 202));
        Assert.False(await db.UserSessions.AnyAsync(session => session.UserId == 202));
        Assert.False(await db.Set<GroupMember>().AnyAsync(member => member.UserId == 202));
        Assert.False(await db.QueryHistory.AnyAsync(entry => entry.UserId == 202));
        Assert.False(await db.SavedPlaylists.AnyAsync(playlist => playlist.UserId == 202));
        Assert.False(await db.StoreClicks.AnyAsync(click => click.UserId == 202));
        var audit = await db.AdminAuditLogs.SingleAsync();
        Assert.Equal(101, audit.AdminUserId);
        Assert.Equal(202, audit.AccountId);
        Assert.InRange(audit.OccurredAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
        Assert.Equal(["Id", "AdminUserId", "OccurredAt", "AccountId"],
            typeof(AdminAuditLog).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task Delete_WhenDatabaseRejectsRemoval_RollsBackAuditAndAccountChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GamifyDbContext>().UseSqlite(connection).Options;
        await using var db = new GamifyDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(
            User(101, "Admin", "admin@gmail.com"),
            User(202, "Player", "player@gmail.com"));
        db.UserSessions.Add(new UserSession { Id = Guid.NewGuid(), UserId = 202, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "AccountBlockers" (
                "UserId" INTEGER NOT NULL REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            INSERT INTO "AccountBlockers" ("UserId") VALUES (202);
            """);

        var result = await Controller(db).Delete(202, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, ((ObjectResult)result).StatusCode);
        Assert.True(await db.Users.AsNoTracking().AnyAsync(user => user.Id == 202));
        Assert.True(await db.UserSessions.AsNoTracking().AnyAsync(session => session.UserId == 202));
        Assert.Empty(await db.AdminAuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM AccountBlockers").SingleAsync());
    }

    private static AdminUsersController Controller(GamifyDbContext db)
    {
        var controller = new AdminUsersController(db, NullLogger<AdminUsersController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, "101"),
                    new Claim(ClaimTypes.Role, "Administrator")
                ], "test"))
            }
        };
        return controller;
    }

    private static User User(int id, string name, string email, DateTimeOffset? registeredAt = null) => new()
    {
        Id = id,
        Name = name,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        PasswordHash = "not-a-real-hash",
        Role = id == 101 ? "Administrator" : "User",
        RegisteredAt = registeredAt ?? DateTimeOffset.UtcNow
    };
}
