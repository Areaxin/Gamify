using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamify.Api.Controllers;

/// <summary>
/// GAM-US12: fiksuoja paspaudimą ant parduotuvės nuorodos. Pati nuoroda atsidaro naršyklėje tiesiogiai, o šis
/// kreipinys siunčiamas lygiagrečiai, todėl statistikos klaida niekada nesutrukdo patekti į parduotuvę.
/// </summary>
[ApiController]
[Authorize]
[Route("api/store-clicks")]
public sealed class StoreClicksController(GamifyDbContext db) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("store-clicks")]
    public async Task<IActionResult> Record(StoreClickRequest request, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        if (request.StoreUrl is not { Length: <= 500 } || StoreLinks.Resolve(request.StoreUrl) is not { } link)
            return NotFound(new { message = "Šis žaidimas neturi patvirtintos parduotuvės nuorodos." });

        db.StoreClicks.Add(new StoreClick
        {
            UserId = userId,
            StoreUrl = link.Url,
            Store = link.Store,
            ClickedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}

public sealed record StoreClickRequest(string? StoreUrl);
