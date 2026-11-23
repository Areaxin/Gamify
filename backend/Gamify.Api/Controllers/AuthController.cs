using System.ComponentModel.DataAnnotations;
using System.Text;
using Gamify.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(GamifyDbContext db, JwtSessions jwt) : ControllerBase
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("gamify-login-check");

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";
        if (name.Length is < 1 or > 80)
            return BadRequest(new { message = "Įveskite vardą (iki 80 simbolių)." });
        if (!IsValidEmail(email))
            return BadRequest(new { message = "Įveskite galiojantį el. pašto adresą (iki 254 simbolių)." });
        if (request.Password is null || request.Password.EnumerateRunes().Count() < 8)
            return BadRequest(new { message = "Slaptažodis turi būti bent 8 simbolių." });
        if (!FitsBcrypt(request.Password))
            return BadRequest(new { message = "Slaptažodis negali viršyti 72 baitų ar turėti nulinio simbolio." });

        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
            return Conflict(new { message = "Šis el. paštas jau naudojamas." });

        db.Users.Add(new User
        {
            Name = name,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            return Conflict(new { message = "Šis el. paštas jau naudojamas." });
        }

        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? "";
        if (!IsValidEmail(email) || string.IsNullOrEmpty(request.Password) || !FitsBcrypt(request.Password))
            return Unauthorized(new { message = "Neteisingi duomenys" });

        var normalizedEmail = email.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash) || user is null)
            return Unauthorized(new { message = "Neteisingi duomenys" });

        var now = DateTime.UtcNow;
        await db.UserSessions.Where(session => session.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, ExpiresAt = now.AddHours(8) };
        var token = jwt.CreateToken(session);
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { token, session.ExpiresAt });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!JwtSessions.TryReadClaims(User, out var userId, out var sessionId))
            return Unauthorized();

        await db.UserSessions.Where(session => session.Id == sessionId && session.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return NoContent();
    }

    private static bool IsValidEmail(string email) =>
        email.Length is > 0 and <= 254 && new EmailAddressAttribute().IsValid(email);

    private static bool FitsBcrypt(string password) =>
        !password.Contains('\0') && Encoding.UTF8.GetByteCount(password) <= 72;
}

public sealed record RegisterRequest(string? Name, string? Email, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
