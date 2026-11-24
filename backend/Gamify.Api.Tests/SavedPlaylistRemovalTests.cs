using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Gamify.Api.Controllers;
using Gamify.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gamify.Api.Tests;

public sealed class SavedPlaylistRemovalTests
{
    [Fact]
    public async Task RemoveGame_PersistsRemovalAndRejectsOtherUsersPlaylist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GamifyDbContext>().UseSqlite(connection).Options;
        await using var db = new GamifyDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var gameOne = new SavedPlaylistGame(1, "Game one", "", 0, ["PC"], 90, "Great fit", 1, null, null, null, null);
        var gameTwo = new SavedPlaylistGame(2, "Game two", "", 0, ["PC"], 80, "Good fit", 1, null, null, null, null);
        db.Users.AddRange(
            new User { Id = 101, Name = "First", Email = "first@example.test", NormalizedEmail = "FIRST", PasswordHash = "x" },
            new User { Id = 102, Name = "Second", Email = "second@example.test", NormalizedEmail = "SECOND", PasswordHash = "x" });
        db.SavedPlaylists.AddRange(
            Playlist(101, "First playlist", gameOne, gameTwo),
            Playlist(102, "Second playlist", gameOne, gameTwo));
        await db.SaveChangesAsync();

        var controller = new SavedPlaylistsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(JwtRegisteredClaimNames.Sub, "101"),
                        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                    ], "test"))
                }
            }
        };

        var ownPlaylist = await db.SavedPlaylists.AsNoTracking().SingleAsync(playlist => playlist.UserId == 101);
        var ownResult = await controller.RemoveGame(ownPlaylist.Id, gameOne.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(ownResult);

        var savedGames = JsonSerializer.Deserialize<SavedPlaylistGame[]>(await db.SavedPlaylists.AsNoTracking()
            .Where(playlist => playlist.Id == ownPlaylist.Id).Select(playlist => playlist.GamesJson).SingleAsync());
        Assert.Equal([gameTwo.Id], savedGames!.Select(game => game.Id));

        var anotherUsersPlaylist = await db.SavedPlaylists.AsNoTracking()
            .SingleAsync(playlist => playlist.UserId == 102);
        var forbiddenResult = await controller.RemoveGame(
            anotherUsersPlaylist.Id, gameOne.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(forbiddenResult);

        var otherUsersGames = JsonSerializer.Deserialize<SavedPlaylistGame[]>(await db.SavedPlaylists.AsNoTracking()
            .Where(playlist => playlist.Id == anotherUsersPlaylist.Id)
            .Select(playlist => playlist.GamesJson).SingleAsync());
        Assert.Equal([gameOne.Id, gameTwo.Id], otherUsersGames!.Select(game => game.Id));
    }

    private static SavedPlaylist Playlist(int userId, string name, params SavedPlaylistGame[] games) => new()
    {
        UserId = userId,
        Name = name,
        GamesJson = JsonSerializer.Serialize(games),
        CreatedAt = DateTimeOffset.UtcNow
    };
}
