using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileController(GamifyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var user = await db.Users.AsNoTracking().Include(user => user.Platforms)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

        return user is null ? Unauthorized() : Ok(ProfileResponse.From(user, user.Role == "Administrator"));
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        var platforms = await db.Platforms.AsNoTracking().OrderBy(platform => platform.Id).ToListAsync(cancellationToken);
        return Ok(new { platforms, pcLevels = PcSpecs.Levels });
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 80)
            return BadRequest(new { message = "Įveskite vardą (iki 80 simbolių)." });

        var platformIds = request.PlatformIds?.Distinct().ToArray() ?? [];
        if (platformIds.Length == 0)
            return BadRequest(new { message = "Pasirinkite bent vieną platformą." });

        var pcLevel = request.PcLevel?.Trim();
        if (platformIds.Contains(Platform.PcId) && PcSpecs.Rank(pcLevel) == 0)
            return BadRequest(new { message = "Pasirinkite PC specifikacijų lygį." });

        var today = AgeRatings.Today;
        if (request.BirthDate is { } birthDate && !AgeRatings.IsValid(birthDate, today))
            return BadRequest(new { message = "Įveskite galiojančią gimimo datą." });
        if (request.PreferredBudgetEur is { } budget &&
            (budget is < 0 or > 1000 || decimal.Round(budget, 2) != budget))
            return BadRequest(new { message = "Pageidaujamas biudžetas turi būti nuo 0 iki 1000 € (iki 2 skaitmenų po kablelio)." });
        if (request.PreferredExperience is { } experience &&
            (!Enum.TryParse<ExperienceLevel>(experience, true, out var level) ||
             !Enum.IsDefined(level) || int.TryParse(experience, out _)))
            return BadRequest(new { message = "Pasirinkite galiojantį patirties lygį." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var platforms = await db.Platforms.Where(platform => platformIds.Contains(platform.Id))
            .ToListAsync(cancellationToken);
        if (platforms.Count != platformIds.Length)
            return BadRequest(new { message = "Pasirinkta nežinoma platforma." });

        var user = await db.Users.Include(user => user.Platforms)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        if (user.BirthDate is { } savedDate && request.BirthDate is { } newDate && savedDate != newDate)
            return BadRequest(new { message = "Išsaugotos gimimo datos pakeisti negalima." });

        user.Name = name;
        user.PcLevel = platformIds.Contains(Platform.PcId) ? pcLevel : null;
        user.Platforms = platforms;
        user.BirthDate ??= request.BirthDate;
        user.PreferredBudgetCents = request.PreferredBudgetEur is { } preferredBudget
            ? (int)(preferredBudget * 100)
            : null;
        user.PreferredExperience = request.PreferredExperience;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ProfileResponse.From(user, user.Role == "Administrator"));
    }
}

public sealed record UpdateProfileRequest(
    string? Name, int[]? PlatformIds, string? PcLevel, DateOnly? BirthDate,
    decimal? PreferredBudgetEur, string? PreferredExperience);

public sealed record ProfileResponse(
    string Name, string Email, int[] PlatformIds, string? PcLevel, DateOnly? BirthDate,
    AgeRatings? AgeRating, decimal? PreferredBudgetEur, string? PreferredExperience, bool IsAdmin)
{
    public static ProfileResponse From(User user, bool isAdmin)
    {
        AgeRatings? ageRating = null;
        if (user.BirthDate is { } birthDate)
            ageRating = AgeRatings.ForAge(AgeRatings.AgeOn(birthDate, AgeRatings.Today));
        return new(user.Name, user.Email, user.Platforms.Select(platform => platform.Id).Order().ToArray(), user.PcLevel,
            user.BirthDate, ageRating, user.PreferredBudgetCents / 100m, user.PreferredExperience, isAdmin);
    }
}
