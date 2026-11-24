using System.Text.Json;
using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/saved-playlists")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SavedPlaylistsController(GamifyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var playlists = await db.SavedPlaylists.AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .OrderByDescending(playlist => playlist.Id)
            .Select(playlist => new SavedPlaylistSummary(
                playlist.Id, playlist.Name, playlist.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(playlists);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var playlist = await db.SavedPlaylists.AsNoTracking()
            .SingleOrDefaultAsync(playlist => playlist.Id == id && playlist.UserId == userId, cancellationToken);

        return playlist is null ? NotFound() : Ok(ToResponse(playlist));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SavePlaylistRequest request, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 100)
            return BadRequest(new { message = "Įveskite grojaraščio pavadinimą (iki 100 simbolių)." });

        var requestedGames = request.Games;
        if (requestedGames is not { Length: > 0 and <= 10 }
            || requestedGames.Any(game => game is null || game.MatchPercentage is < 0 or > 100
                || string.IsNullOrWhiteSpace(game.Explanation) || game.Explanation.Length > 1000)
            || requestedGames.Select(game => game.Id).Distinct().Count() != requestedGames.Length)
            return BadRequest(new { message = "Grojaraštyje turi būti nuo 1 iki 10 galiojančių žaidimų." });

        var user = await db.Users.AsNoTracking().Include(user => user.Platforms)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();
        if (user.BirthDate is not { } birthDate)
            return BadRequest(new { message = "Pirmiausia profilyje nurodykite gimimo datą." });

        var gameIds = requestedGames.Select(game => game.Id).ToArray();
        var games = await db.Games.AsNoTracking().Where(game => gameIds.Contains(game.Id))
            .Include(game => game.Platforms).ToListAsync(cancellationToken);
        if (games.Count != gameIds.Length)
            return BadRequest(new { message = "Grojaraštyje yra nežinomų žaidimų." });

        var platformIds = user.Platforms.Select(platform => platform.Id).ToArray();
        var pcLevel = PcSpecs.Rank(user.PcLevel);
        var age = AgeRatings.AgeOn(birthDate, AgeRatings.Today);
        var ageApprovedGames = games.Where(game =>
            (game.PegiAge != null || game.EsrbAge != null) &&
            (game.PegiAge == null || game.PegiAge <= age) &&
            (game.EsrbAge == null || game.EsrbAge <= age) &&
            game.Platforms.Any(platform => platformIds.Contains(platform.Id) &&
                (platform.Id != Platform.PcId || game.MinimumPcLevel <= pcLevel))).ToArray();
        if (ageApprovedGames.Length != games.Count)
            return BadRequest(new { message = "Kai kurie žaidimai neatitinka profilio amžiaus ar platformų." });

        var gameInputs = requestedGames.ToDictionary(game => game.Id);
        var playlistGames = ageApprovedGames
            .OrderBy(game => Array.IndexOf(gameIds, game.Id))
            .Select(game =>
            {
                var input = gameInputs[game.Id];
                var platforms = game.Platforms.Where(platform => platformIds.Contains(platform.Id) &&
                        (platform.Id != Platform.PcId || game.MinimumPcLevel <= pcLevel))
                    .OrderBy(platform => platform.Id).Select(platform => platform.Name).ToArray();
                var store = StoreLinks.Resolve(game.StoreUrl);

                return new SavedPlaylistGame(game.Id, game.Title, GameCoverImages.For(game.Id, game.Title),
                    game.PriceCents, platforms, input.MatchPercentage, input.Explanation.Trim(), game.MaxPlayers,
                    game.PegiAge, AgeRatings.EsrbLabel(game.EsrbAge), store?.Url, store?.Store);
            }).ToArray();

        var playlist = new SavedPlaylist
        {
            UserId = userId,
            Name = name,
            GamesJson = JsonSerializer.Serialize(playlistGames),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.SavedPlaylists.Add(playlist);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = playlist.Id }, ToResponse(playlist));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, RenamePlaylistRequest request, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 100)
            return BadRequest(new { message = "Įveskite grojaraščio pavadinimą (iki 100 simbolių)." });

        var playlist = await db.SavedPlaylists
            .SingleOrDefaultAsync(playlist => playlist.Id == id && playlist.UserId == userId, cancellationToken);
        if (playlist is null)
            return NotFound();

        playlist.Name = name;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new SavedPlaylistSummary(playlist.Id, playlist.Name, playlist.CreatedAt));
    }

    [HttpDelete("{id:int}/games/{gameId:int}")]
    public async Task<IActionResult> RemoveGame(int id, int gameId, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var playlist = await db.SavedPlaylists
            .SingleOrDefaultAsync(playlist => playlist.Id == id && playlist.UserId == userId, cancellationToken);
        if (playlist is null)
            return NotFound();

        var games = JsonSerializer.Deserialize<SavedPlaylistGame[]>(playlist.GamesJson)
            ?? throw new JsonException($"Saved playlist {playlist.Id} contains invalid game data.");
        var remainingGames = games.Where(game => game.Id != gameId).ToArray();
        if (remainingGames.Length == games.Length)
            return NotFound();

        playlist.GamesJson = JsonSerializer.Serialize(remainingGames);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var deleted = await db.SavedPlaylists
            .Where(playlist => playlist.Id == id && playlist.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFound() : NoContent();
    }

    private static SavedPlaylistResponse ToResponse(SavedPlaylist playlist) =>
        new(playlist.Id, playlist.Name, playlist.CreatedAt,
            JsonSerializer.Deserialize<SavedPlaylistGame[]>(playlist.GamesJson)
            ?? throw new JsonException($"Saved playlist {playlist.Id} contains invalid game data."));
}

public sealed record SavePlaylistGameRequest(int Id, int MatchPercentage, string Explanation);
public sealed record SavePlaylistRequest(string? Name, SavePlaylistGameRequest[]? Games);
public sealed record RenamePlaylistRequest(string? Name);
public sealed record SavedPlaylistSummary(int Id, string Name, DateTimeOffset CreatedAt);
public sealed record SavedPlaylistResponse(
    int Id, string Name, DateTimeOffset CreatedAt, SavedPlaylistGame[] Games);
