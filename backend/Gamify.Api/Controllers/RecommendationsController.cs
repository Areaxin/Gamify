using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Gamify.Api.Ai;
using Gamify.Api.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recommendations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RecommendationsController(GamifyDbContext db, OpenAiResponsesClient ai, IMemoryCache cache) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("recommendations")]
    public async Task<IActionResult> Search(RecommendationRequest request, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Aprašykite, kokio žaidimo ieškote." });
        if (request.Text.EnumerateRunes().Count() > 1000)
            return BadRequest(new { message = "Aprašymas negali viršyti 1000 simbolių." });

        var user = await db.Users.AsNoTracking().Include(user => user.Platforms).Include(user => user.GroupMembers)
            .AsSplitQuery().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();
        if (user.BirthDate is not { } birthDate)
            return BadRequest(new { message = "Pirmiausia profilyje nurodykite gimimo datą." });
        if (user.Platforms.Count == 0)
            return BadRequest(new { message = "Pirmiausia profilyje pasirinkite turimas platformas." });
        if (request.ForGroup && user.GroupMembers.Count == 0)
            return BadRequest(new { message = "Pirmiausia profilyje pridėkite grupės narių." });
        if (!ai.IsConfigured)
            return StatusCode(503, new { message = "Rekomendacijoms dar nesukonfigūruotas OpenAI raktas arba modelis." });

        var cacheInput = JsonSerializer.Serialize(new
        {
            userId,
            text = request.Text.Trim(),
            request.ForGroup,
            birthDate,
            platforms = user.Platforms.Select(platform => platform.Id).Order().ToArray(),
            pcLevel = user.PcLevel,
            groupBirthDates = user.GroupMembers.Select(member => member.BirthDate).Order().ToArray()
        });
        var cacheKey = "recommendations:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheInput)));
        if (cache.TryGetValue<RecommendationResponse>(cacheKey, out var cached))
            return Ok(cached);

        var youngest = request.ForGroup
            ? user.GroupMembers.Select(member => member.BirthDate).Append(birthDate).Max()
            : birthDate;
        var age = AgeRatings.AgeOn(youngest, AgeRatings.Today);
        RecommendationPrompt filters;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var text = await ai.GetStructuredTextAsync(request.Text.Trim(), RecommendationPrompt.Instructions,
                "game_filters", RecommendationPrompt.Schema, timeout.Token);
            filters = RecommendationPrompt.Parse(text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "DI atsakymas užtruko per ilgai. Pabandykite dar kartą." });
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException)
        {
            return StatusCode(502, new { message = "Nepavyko išanalizuoti aprašymo. Pabandykite dar kartą." });
        }

        var platformIds = user.Platforms.Select(platform => platform.Id).ToArray();
        var pcLevel = PcSpecs.Rank(user.PcLevel);
        var query = db.Games.AsNoTracking().Where(game =>
            (game.PegiAge != null || game.EsrbAge != null) &&
            (game.PegiAge == null || game.PegiAge <= age) &&
            (game.EsrbAge == null || game.EsrbAge <= age) &&
            game.Platforms.Any(platform => platformIds.Contains(platform.Id) &&
                (platform.Id != Platform.PcId || game.MinimumPcLevel <= pcLevel)));

        if (filters.MaxBudgetCents is { } budget)
            query = query.Where(game => game.PriceCents <= budget);
        var groupSize = request.ForGroup ? user.GroupMembers.Count + 1 : 1;
        var playerCount = Math.Max(filters.PlayerCount ?? 1, groupSize);
        query = query.Where(game => game.MaxPlayers >= playerCount);
        if (Enum.TryParse<GameMood>(filters.Mood, ignoreCase: true, out var mood))
            query = query.Where(game => (game.Moods & mood) != 0);
        var difficulty = filters.Experience switch { "beginner" => 1, "intermediate" => 2, _ => 3 };
        query = query.Where(game => game.Difficulty <= difficulty);

        var games = await query.OrderBy(game => game.PriceCents).ThenBy(game => game.Title).ThenBy(game => game.Id)
            .Take(10).Include(game => game.Platforms).ToListAsync(cancellationToken);
        if (games.Count < 5)
        {
            var empty = new RecommendationResponse([], "Nieko nerasta.");
            cache.Set(cacheKey, empty, TimeSpan.FromMinutes(10));
            return Ok(empty);
        }

        var result = new RecommendationResponse(
            games.Select(game => new RecommendedGame(game.Id, game.Title, game.PriceCents, game.MaxPlayers,
                game.PegiAge, AgeRatings.EsrbLabel(game.EsrbAge), game.Platforms.Where(platform =>
                    platformIds.Contains(platform.Id) &&
                    (platform.Id != Platform.PcId || game.MinimumPcLevel <= pcLevel))
                    .OrderBy(platform => platform.Id).Select(platform => platform.Name).ToArray())).ToArray());
        cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
        return Ok(result);
    }
}

public sealed record RecommendationRequest(string? Text, bool ForGroup = false);

public sealed record RecommendedGame(
    int Id, string Title, int PriceCents, int MaxPlayers, int? Pegi, string? Esrb, string[] Platforms);

public sealed record RecommendationResponse(RecommendedGame[] Games, string? Message = null);
