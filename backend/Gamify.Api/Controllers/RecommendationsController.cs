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

        if (request.Filters?.Validate() is { } filterError)
            return BadRequest(new { message = filterError });
        var searchText = request.Text.Trim();
        var searchedAt = DateTime.UtcNow;

        var user = await db.Users.AsNoTracking().Include(user => user.Platforms)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();
        if (user.BirthDate is not { } birthDate)
            return BadRequest(new { message = "Pirmiausia profilyje nurodykite gimimo datą." });
        if (user.Platforms.Count == 0)
            return BadRequest(new { message = "Pirmiausia profilyje pasirinkite turimas platformas." });
        GroupRecommendationProfile? groupProfile = null;
        if (request.ForGroup)
        {
            if (request.GroupId is not { } groupId)
                return BadRequest(new { message = "Pasirinkite grupę." });

            var group = await db.UserGroups.AsNoTracking()
                .Include(item => item.Members)
                .ThenInclude(member => member.User)
                .ThenInclude(memberUser => memberUser.Platforms)
                .AsSplitQuery()
                .SingleOrDefaultAsync(item => item.Id == groupId && item.OwnerUserId == userId, cancellationToken);
            if (group is null)
                return NotFound(new { message = "Grupė nerasta." });

            var groupUsers = group.Members.Select(member => member.User).ToArray();
            var incompleteProfiles = groupUsers.Where(member =>
                member.BirthDate is null || member.Platforms.Count == 0 ||
                member.Platforms.Any(platform => platform.Id == Platform.PcId) &&
                PcSpecs.Rank(member.PcLevel) == 0).ToArray();
            if (incompleteProfiles.Length > 0)
            {
                var names = string.Join(", ", incompleteProfiles.Select(member => member.Name));
                return BadRequest(new
                {
                    message = $"Grupės nariai turi užpildyti gimimo datą, platformas ir PC specifikacijų lygį savo profilyje: {names}."
                });
            }
            groupProfile = GroupRecommendationProfile.FromMembers(groupUsers, userId, AgeRatings.Today);
        }

        var selectedPlatformIds = request.Filters?.PlatformIds?.Distinct().ToArray() ?? [];
        var selectedPlatforms = selectedPlatformIds.Length == 0
            ? []
            : await db.Platforms.AsNoTracking().Where(platform => selectedPlatformIds.Contains(platform.Id))
                .OrderBy(platform => platform.Id).ToListAsync(cancellationToken);
        if (selectedPlatforms.Count != selectedPlatformIds.Length)
            return BadRequest(new { message = "Pasirinkta nežinoma platforma." });

        List<Platform> requesterPlatforms;
        int pcLevel;
        int age;
        int groupSize;
        GroupMemberConstraint[] participants;
        HashSet<int> sharedPlatformIds;
        if (request.ForGroup)
        {
            var savedGroup = groupProfile!;
            sharedPlatformIds = savedGroup.SharedPlatformIds;
            if (selectedPlatformIds.Length > 0)
                sharedPlatformIds.IntersectWith(selectedPlatformIds);

            requesterPlatforms = await db.Platforms.AsNoTracking()
                .Where(platform => sharedPlatformIds.Contains(platform.Id))
                .OrderBy(platform => platform.Id).ToListAsync(cancellationToken);
            age = savedGroup.Age;
            pcLevel = savedGroup.PcLevel;
            groupSize = savedGroup.GroupSize;
            participants = savedGroup.Participants;
        }
        else
        {
            requesterPlatforms = selectedPlatforms.Count > 0 ? selectedPlatforms : user.Platforms;
            pcLevel = PcSpecs.Rank(user.PcLevel);
            if (pcLevel == 0 && requesterPlatforms.Any(platform => platform.Id == Platform.PcId))
                pcLevel = PcSpecs.Levels.Length;
            age = AgeRatings.AgeOn(birthDate, AgeRatings.Today);
            groupSize = 1;
            participants = [];
            sharedPlatformIds = requesterPlatforms.Select(platform => platform.Id).ToHashSet();
        }

        SavedPlaylistGame[] recommendations;
        GroupConstraintBlocker[] blockingMembers;
        string? emptyMessage = null;
        if (request.ForGroup && sharedPlatformIds.Count == 0)
        {
            recommendations = [];
            blockingMembers = participants.Select(participant =>
                new GroupConstraintBlocker(participant.Name, ["platformos"])).ToArray();
            emptyMessage = "Nėra bendros platformos, kurią turėtų visi paieškos dalyviai. Patikrinkite kiekvieno nario platformų pasirinkimą.";
        }
        else
        {
            if (!ai.IsConfigured)
                return StatusCode(503, new { message = "Rekomendacijoms dar nesukonfigūruotas OpenAI raktas arba modelis." });

            var platforms = requesterPlatforms;
            var overrides = request.Filters is { } filters
                ? new FilterOverrides(filters.MaxBudgetCents, filters.PlayerCount, filters.ExperienceLevelValue)
                : null;
            try
            {
                var result = await search.SearchAsync(searchText,
                    platforms.Select(platform => platform.Name).ToArray(), pcLevel,
                    age, groupSize, cancellationToken, overrides, participants);
                recommendations = result.Games;
                blockingMembers = result.BlockingMembers;
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
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "QueryHistory" ("UserId", "Text", "ForGroup", "CreatedAt")
            VALUES ({userId}, {searchText}, {request.ForGroup}, {searchedAt})
            ON CONFLICT ("UserId", "Text", "ForGroup") DO UPDATE SET
                "CreatedAt" = MAX("QueryHistory"."CreatedAt", excluded."CreatedAt")
            """, cancellationToken);

        if (recommendations.Length == 0)
            emptyMessage ??= "Nieko nerasta.";

        return Ok(new
        {
            games = recommendations,
            message = emptyMessage,
            blockingMembers,
            recommendationToken = recommendations.Length > 0 ? tickets.Create(userId, recommendations) : null
        });
    }

}

public sealed record RecommendationRequest(
    string? Text, bool ForGroup = false, int? GroupId = null, RecommendationFilters? Filters = null);
