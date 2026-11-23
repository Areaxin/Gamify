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
    private static readonly string[] PcLevels = ["Žemas", "Vidutinis", "Aukštas"];

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var user = await db.Users.AsNoTracking().Include(user => user.Platforms)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

        return user is null ? Unauthorized() : Ok(ProfileResponse.From(user));
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        var platforms = await db.Platforms.AsNoTracking().OrderBy(platform => platform.Id).ToListAsync(cancellationToken);
        return Ok(new { platforms, pcLevels = PcLevels });
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
        if (platformIds.Contains(Platform.PcId) && !PcLevels.Contains(pcLevel))
            return BadRequest(new { message = "Pasirinkite PC specifikacijų lygį." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var platforms = await db.Platforms.Where(platform => platformIds.Contains(platform.Id))
            .ToListAsync(cancellationToken);
        if (platforms.Count != platformIds.Length)
            return BadRequest(new { message = "Pasirinkta nežinoma platforma." });

        var user = await db.Users.Include(user => user.Platforms)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        user.Name = name;
        user.PcLevel = platformIds.Contains(Platform.PcId) ? pcLevel : null;
        user.Platforms = platforms;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ProfileResponse.From(user));
    }
}

public sealed record UpdateProfileRequest(string? Name, int[]? PlatformIds, string? PcLevel);

public sealed record ProfileResponse(string Name, string Email, int[] PlatformIds, string? PcLevel)
{
    public static ProfileResponse From(User user) =>
        new(user.Name, user.Email, user.Platforms.Select(platform => platform.Id).Order().ToArray(), user.PcLevel);
}
