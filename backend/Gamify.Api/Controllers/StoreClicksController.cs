using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

/// <summary>
/// GAM-US12: fiksuoja paspaudimą ant parduotuvės nuorodos. Pati nuoroda atsidaro naršyklėje tiesiogiai, o šis
/// kreipinys siunčiamas lygiagrečiai, todėl statistikos klaida niekada nesutrukdo patekti į parduotuvę.
/// </summary>
[ApiController]
[Authorize]
[Route("api/games/{gameId:int}/store-clicks")]
public sealed class StoreClicksController(GamifyDbContext db) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("store-clicks")]
    public async Task<IActionResult> Record(int gameId, CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out _))
            return Unauthorized();

        var game = await db.Games.AsNoTracking().Where(game => game.Id == gameId)
            .Select(game => new { game.StoreUrl }).SingleOrDefaultAsync(cancellationToken);
        if (game is null)
            return NotFound(new { message = "Žaidimas nerastas." });
        if (StoreLinks.Resolve(game.StoreUrl) is not { } link)
            return NotFound(new { message = "Šis žaidimas neturi patvirtintos parduotuvės nuorodos." });

        db.StoreClicks.Add(new StoreClick
        {
            UserId = userId,
            GameId = gameId,
            Store = link.Store,
            ClickedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
