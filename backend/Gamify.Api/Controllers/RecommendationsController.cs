using System.Text;
using System.Text.Json;
using Gamify.Api.Ai;
using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recommendations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RecommendationsController(GamifyDbContext db, OpenAiResponsesClient ai) : ControllerBase
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

        var searchText = request.Text.Trim();
        var searchedAt = DateTime.UtcNow;

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

        var youngest = request.ForGroup
            ? user.GroupMembers.Select(member => member.BirthDate).Append(birthDate).Max()
            : birthDate;
        var age = AgeRatings.AgeOn(youngest, AgeRatings.Today);
        RecommendationPrompt filters;
        try
        {
            var text = await ai.GetStructuredTextAsync(searchText, RecommendationPrompt.Instructions,
                "game_filters", RecommendationPrompt.Schema, cancellationToken);
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

        var games = await query.Include(game => game.Platforms).ToListAsync(cancellationToken);
        int? requestedPlayerCount = filters.PlayerCount is not null || request.ForGroup ? playerCount : null;
        var recommendations = games.Select(game =>
            {
                var match = RecommendationScoring.Calculate(game, filters.Mood, filters.MaxBudgetCents,
                    requestedPlayerCount, filters.Experience);
                var platforms = game.Platforms.Where(platform =>
                        platformIds.Contains(platform.Id) &&
                        (platform.Id != Platform.PcId || game.MinimumPcLevel <= pcLevel))
                    .OrderBy(platform => platform.Id).Select(platform => platform.Name).ToArray();
                var store = StoreLinks.Resolve(game.StoreUrl);

                return new RecommendedGame(game.Id, game.Title, GameCoverImages.For(game.Id, game.Title),
                    game.PriceCents, platforms, match.Percentage, match.Explanation, game.MaxPlayers, game.PegiAge,
                    AgeRatings.EsrbLabel(game.EsrbAge), store?.Url, store?.Store);
            })
            .OrderByDescending(game => game.MatchPercentage)
            .ThenBy(game => game.Title)
            .ThenBy(game => game.Id)
            .Take(10)
            .ToArray();

        db.QueryHistory.Add(new QueryHistoryEntry { UserId = userId, Text = searchText, CreatedAt = searchedAt });
        await db.SaveChangesAsync(cancellationToken);

        if (recommendations.Length == 0)
            return Ok(new
            {
                games = recommendations,
                message = "Nieko nerasta."
            });

        return Ok(new { games = recommendations });
    }
}

public sealed record RecommendationRequest(string? Text, bool ForGroup = false);

/// <param name="StoreUrl">Tik iš <see cref="StoreLinks.AllowedHosts"/> domenų; null, jei nuoroda nežinoma.</param>
public sealed record RecommendedGame(
    int Id, string Title, string CoverImageUrl, int PriceCents, string[] Platforms, int MatchPercentage,
    string Explanation, int MaxPlayers, int? Pegi, string? Esrb, string? StoreUrl, string? StoreName);