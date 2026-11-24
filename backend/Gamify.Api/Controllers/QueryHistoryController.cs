using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile/query-history")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class QueryHistoryController(GamifyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var entries = await db.QueryHistory.AsNoTracking().Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.CreatedAt).ThenByDescending(entry => entry.Id)
            .Select(entry => new
            {
                entry.Id,
                entry.Text,
                entry.ForGroup,
                CreatedAt = DateTime.SpecifyKind(entry.CreatedAt, DateTimeKind.Utc)
            })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var deleted = await db.QueryHistory.Where(entry => entry.Id == id && entry.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFound() : NoContent();
    }
}
