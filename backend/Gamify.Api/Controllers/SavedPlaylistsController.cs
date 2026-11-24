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
public sealed class SavedPlaylistsController(
    GamifyDbContext db, RecommendationTickets tickets, LiveStoreClient stores) : ControllerBase
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

        if (playlist is null)
            return NotFound();

        var games = JsonSerializer.Deserialize<SavedPlaylistGame[]>(playlist.GamesJson) ?? [];
        return Ok(ToResponse(playlist, await RefreshPrices(games, cancellationToken)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SavePlaylistRequest request, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 100)
            return BadRequest(new { message = "Įveskite grojaraščio pavadinimą (iki 100 simbolių)." });

        var games = tickets.Read(request.RecommendationToken, userId);
        if (games is not { Length: > 0 and <= 10 })
            return BadRequest(new { message = "Paieškos rezultatai nebegalioja. Ieškokite dar kartą." });

        var user = await db.Users.AsNoTracking().Include(user => user.Platforms)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();
        if (user.BirthDate is not { } birthDate)
            return BadRequest(new { message = "Pirmiausia profilyje nurodykite gimimo datą." });

        var platforms = user.Platforms.Select(platform => platform.Name).ToHashSet(StringComparer.Ordinal);
        var pcLevel = PcSpecs.Rank(user.PcLevel);
        var age = AgeRatings.AgeOn(birthDate, AgeRatings.Today);
        if (games.Any(game => game.Pegi > age ||
            (game.Esrb switch { "E10+" => 10, "T" => 13, "M" => 17, "AO" => 18, _ => 0 }) > age ||
            !game.Platforms.Any(platform => platforms.Contains(platform) &&
                (platform != "PC" || game.MinimumPcLevel <= pcLevel))))
            return BadRequest(new { message = "Kai kurie žaidimai neatitinka profilio amžiaus ar platformų." });

        var playlist = new SavedPlaylist
        {
            UserId = userId,
            Name = name,
            GamesJson = JsonSerializer.Serialize(games),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.SavedPlaylists.Add(playlist);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = playlist.Id }, ToResponse(playlist, games));
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

    private Task<SavedPlaylistGame[]> RefreshPrices(SavedPlaylistGame[] games, CancellationToken cancellationToken) =>
        Task.WhenAll(games.Select(async game =>
        {
            var store = StoreLinks.Resolve(game.StoreUrl);
            var details = store is null ? null : await stores.CheckAsync(store, cancellationToken);
            return game with { PriceCents = details?.PriceCents };
        }));

    private static SavedPlaylistResponse ToResponse(SavedPlaylist playlist, SavedPlaylistGame[] games) =>
        new(playlist.Id, playlist.Name, playlist.CreatedAt, games);
}

public sealed record SavePlaylistRequest(string? Name, string? RecommendationToken);
public sealed record RenamePlaylistRequest(string? Name);
public sealed record SavedPlaylistSummary(int Id, string Name, DateTimeOffset CreatedAt);
public sealed record SavedPlaylistResponse(
    int Id, string Name, DateTimeOffset CreatedAt, SavedPlaylistGame[] Games);
