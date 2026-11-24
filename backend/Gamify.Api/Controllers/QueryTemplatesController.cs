using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/query-templates")]
public sealed class QueryTemplatesController(GamifyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var templates = await db.QueryTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.SortOrder)
            .Select(template => new
            {
                template.Id,
                template.Title,
                template.Text
            })
            .ToListAsync(cancellationToken);

        return Ok(templates);
    }
}