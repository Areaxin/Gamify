using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserGroupsController(GamifyDbContext db) : ControllerBase
{
    private const int MaxMembers = 5;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var groups = await OwnedGroups(userId).OrderBy(group => group.Id).ToListAsync(cancellationToken);
        return Ok(groups.Select(GroupResponse.From));
    }

    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? query, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var search = query?.Trim() ?? "";
        if (search.Length is < 2 or > 80)
            return BadRequest(new { message = "Paieškai įveskite nuo 2 iki 80 simbolių." });

        var normalizedSearch = search.ToUpperInvariant();
        var caseFoldedSearch = search.ToLowerInvariant();
        var users = await db.Users.AsNoTracking()
            .Where(user => user.Id != userId &&
                (user.Name.ToLower().Contains(caseFoldedSearch) || user.NormalizedEmail.Contains(normalizedSearch)))
            .OrderBy(user => user.Name).Take(10)
            .Select(user => new UserSearchResult(user.Id, user.Name, user.Email))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 80)
            return BadRequest(new { message = "Grupės pavadinimas turi būti nuo 1 iki 80 simbolių." });

        var group = new UserGroup
        {
            OwnerUserId = userId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            Members = [new UserGroupMember { UserId = userId, AddedAt = DateTimeOffset.UtcNow }]
        };
        db.UserGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);

        var created = await OwnedGroups(userId).SingleAsync(item => item.Id == group.Id, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, GroupResponse.From(created));
    }

    [HttpPut("{groupId:int}")]
    public async Task<IActionResult> Rename(int groupId, RenameGroupRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 80)
            return BadRequest(new { message = "Grupės pavadinimas turi būti nuo 1 iki 80 simbolių." });

        var group = await db.UserGroups.SingleOrDefaultAsync(
            item => item.Id == groupId && item.OwnerUserId == userId, cancellationToken);
        if (group is null)
            return NotFound();

        group.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{groupId:int}/members")]
    public async Task<IActionResult> AddMember(int groupId, AddGroupMemberRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (request.UserId <= 0 || request.UserId == userId)
            return BadRequest(new { message = "Pasirinkite kitą registruotą naudotoją." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var group = await db.UserGroups.Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == groupId && item.OwnerUserId == userId, cancellationToken);
        if (group is null)
            return NotFound();
        if (group.Members.Any(member => member.UserId == request.UserId))
            return Conflict(new { message = "Šis naudotojas jau yra grupėje." });
        if (group.Members.Count >= MaxMembers)
            return Conflict(new { message = $"Grupėje gali būti ne daugiau kaip {MaxMembers} nariai, įskaitant jus." });
        if (!await db.Users.AnyAsync(user => user.Id == request.UserId, cancellationToken))
            return NotFound(new { message = "Naudotojas nerastas." });

        group.Members.Add(new UserGroupMember
        {
            UserId = request.UserId,
            AddedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{groupId:int}/members/{memberUserId:int}")]
    public async Task<IActionResult> RemoveMember(int groupId, int memberUserId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (memberUserId == userId)
            return BadRequest(new { message = "Grupės kūrėjo pašalinti negalima. Jei reikia, ištrinkite visą grupę." });

        var member = await db.UserGroupMembers
            .Where(item => item.GroupId == groupId && item.UserId == memberUserId &&
                item.Group.OwnerUserId == userId)
            .SingleOrDefaultAsync(cancellationToken);
        if (member is null)
            return NotFound();

        db.UserGroupMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{groupId:int}")]
    public async Task<IActionResult> Delete(int groupId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var group = await db.UserGroups.SingleOrDefaultAsync(
            item => item.Id == groupId && item.OwnerUserId == userId, cancellationToken);
        if (group is null)
            return NotFound();

        db.UserGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<UserGroup> OwnedGroups(int userId) =>
        db.UserGroups.AsNoTracking().Where(group => group.OwnerUserId == userId)
            .Include(group => group.Members)
            .ThenInclude(member => member.User)
            .ThenInclude(user => user.Platforms)
            .AsSplitQuery();

    private bool TryGetUserId(out int userId) =>
        JwtSessions.TryReadClaims(User, out userId, out _);
}

public sealed record CreateGroupRequest(string? Name);
public sealed record RenameGroupRequest(string? Name);
public sealed record AddGroupMemberRequest(int UserId);
public sealed record UserSearchResult(int Id, string Name, string Email);

public sealed record GroupResponse(int Id, string Name, int OwnerUserId, GroupMemberResponse[] Members)
{
    public static GroupResponse From(UserGroup group) =>
        new(group.Id, group.Name, group.OwnerUserId,
            group.Members.OrderBy(member => member.AddedAt).ThenBy(member => member.Id)
                .Select(member => GroupMemberResponse.From(member.User)).ToArray());
}

public sealed record GroupMemberResponse(
    int Id, string Name, string Email, int[] PlatformIds, string[] Platforms,
    string? PcLevel, DateOnly? BirthDate, AgeRatings? AgeRating,
    decimal? PreferredBudgetEur, string? PreferredExperience)
{
    public static GroupMemberResponse From(User user)
    {
        var ageRating = user.BirthDate is { } birthDate
            ? AgeRatings.ForAge(AgeRatings.AgeOn(birthDate, AgeRatings.Today))
            : null;
        return new(user.Id, user.Name, user.Email,
            user.Platforms.Select(platform => platform.Id).Order().ToArray(),
            user.Platforms.OrderBy(platform => platform.Id).Select(platform => platform.Name).ToArray(),
            user.PcLevel, user.BirthDate, ageRating, user.PreferredBudgetCents / 100m, user.PreferredExperience);
    }
}
