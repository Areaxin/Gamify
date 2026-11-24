using System.Security.Claims;
using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/admin/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminUsersController(
    GamifyDbContext db,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new AdminUserResponse(user.Id, user.Name, user.Email, user.RegisteredAt))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpDelete("{accountId:int}")]
    public async Task<IActionResult> Delete(int accountId, CancellationToken cancellationToken)
    {
        var adminIdClaim = User.FindFirstValue("sub");
        if (!int.TryParse(adminIdClaim, out var adminId))
            return Unauthorized();

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var account = await db.Users.SingleOrDefaultAsync(user => user.Id == accountId, cancellationToken);
            if (account is null)
                return NotFound();

            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminUserId = adminId,
                AccountId = account.Id,
                OccurredAt = DateTimeOffset.UtcNow
            });
            db.Users.Remove(account);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return NoContent();
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Administrator {AdminId} failed to delete account {AccountId}", adminId, accountId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Nepavyko ištrinti paskyros. Duomenys nebuvo pakeisti." });
        }
    }
}

public sealed record AdminUserResponse(int Id, string Username, string Email, DateTimeOffset RegisteredAt);
