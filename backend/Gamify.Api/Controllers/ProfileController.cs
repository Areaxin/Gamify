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
    private const int MaxGroupMembers = 11;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var user = await db.Users.AsNoTracking().Include(user => user.Platforms)
            .Include(user => user.GroupMembers).AsSplitQuery()
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

        return user is null ? Unauthorized() : Ok(ProfileResponse.From(user));
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        var platforms = await db.Platforms.AsNoTracking().OrderBy(platform => platform.Id).ToListAsync(cancellationToken);
        return Ok(new { platforms, pcLevels = PcSpecs.Levels, maxGroupMembers = MaxGroupMembers });
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
        if (request.GroupBirthDates is { Length: > MaxGroupMembers })
            return BadRequest(new { message = "Galite pridėti iki 11 grupės narių." });
        if (request.GroupBirthDates?.Any(date => !AgeRatings.IsValid(date, today)) == true)
            return BadRequest(new { message = "Įveskite galiojančias grupės narių gimimo datas." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var platforms = await db.Platforms.Where(platform => platformIds.Contains(platform.Id))
            .ToListAsync(cancellationToken);
        if (platforms.Count != platformIds.Length)
            return BadRequest(new { message = "Pasirinkta nežinoma platforma." });

        var user = await db.Users.Include(user => user.Platforms).Include(user => user.GroupMembers).AsSplitQuery()
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        if (user.BirthDate is { } savedDate && request.BirthDate is { } newDate && savedDate != newDate)
            return BadRequest(new { message = "Išsaugotos gimimo datos pakeisti negalima." });

        user.Name = name;
        user.PcLevel = platformIds.Contains(Platform.PcId) ? pcLevel : null;
        user.Platforms = platforms;
        user.BirthDate ??= request.BirthDate;
        if (request.GroupBirthDates is { } groupDates)
        {
            if (user.BirthDate is null && groupDates.Length > 0)
                return BadRequest(new { message = "Prieš pridėdami grupės narius įrašykite savo gimimo datą." });

            var savedGroupDates = user.GroupMembers.OrderBy(member => member.Id).Select(member => member.BirthDate);
            if (!savedGroupDates.SequenceEqual(groupDates))
                user.GroupMembers = groupDates.Select(date => new GroupMember { BirthDate = date }).ToList();
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ProfileResponse.From(user));
    }
}

public sealed record UpdateProfileRequest(
    string? Name, int[]? PlatformIds, string? PcLevel, DateOnly? BirthDate, DateOnly[]? GroupBirthDates);

public sealed record ProfileResponse(
    string Name, string Email, int[] PlatformIds, string? PcLevel, DateOnly? BirthDate,
    DateOnly[] GroupBirthDates, AgeRatings? AgeRating, AgeRatings? GroupAgeRating)
{
    public static ProfileResponse From(User user)
    {
        var groupBirthDates = user.GroupMembers.OrderBy(member => member.Id).Select(member => member.BirthDate).ToArray();
        var today = AgeRatings.Today;
        AgeRatings? ageRating = null;
        AgeRatings? groupAgeRating = null;
        if (user.BirthDate is { } birthDate)
        {
            ageRating = AgeRatings.ForAge(AgeRatings.AgeOn(birthDate, today));
            if (groupBirthDates.Length > 0)
                groupAgeRating = AgeRatings.ForAge(AgeRatings.AgeOn(groupBirthDates.Append(birthDate).Max(), today));
        }
        return new(user.Name, user.Email, user.Platforms.Select(platform => platform.Id).Order().ToArray(), user.PcLevel,
            user.BirthDate, groupBirthDates, ageRating, groupAgeRating);
    }
}
