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
public sealed class RecommendationsController(
    GamifyDbContext db, OpenAiResponsesClient ai, LiveRecommendations search, RecommendationTickets tickets) : ControllerBase
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
        var groupSize = request.ForGroup ? user.GroupMembers.Count + 1 : 1;
        SavedPlaylistGame[] recommendations;
        try
        {
            recommendations = await search.SearchAsync(searchText,
                user.Platforms.Select(platform => platform.Name).ToArray(), PcSpecs.Rank(user.PcLevel),
                age, groupSize, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "DI atsakymas užtruko per ilgai. Pabandykite dar kartą." });
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException)
        {
            return StatusCode(502, new { message = "Nepavyko gauti žaidimų iš interneto paieškos. Pabandykite dar kartą." });
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "QueryHistory" ("UserId", "Text", "ForGroup", "CreatedAt")
            VALUES ({userId}, {searchText}, {request.ForGroup}, {searchedAt})
            ON CONFLICT ("UserId", "Text", "ForGroup") DO UPDATE SET
                "CreatedAt" = MAX("QueryHistory"."CreatedAt", excluded."CreatedAt")
            """, cancellationToken);

        if (recommendations.Length == 0)
            return Ok(new
            {
                games = recommendations,
                message = "Nieko nerasta."
            });

        return Ok(new { games = recommendations, recommendationToken = tickets.Create(userId, recommendations) });
    }
}

public sealed record RecommendationRequest(string? Text, bool ForGroup = false);
