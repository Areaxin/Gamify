using System.Security.Cryptography;
using System.Text.Json;
using Gamify.Api.Data;
using Microsoft.AspNetCore.DataProtection;

namespace Gamify.Api;

public sealed class RecommendationTickets(IDataProtectionProvider protection)
{
    private readonly ITimeLimitedDataProtector protector =
        protection.CreateProtector("Gamify.SavedPlaylist.v1").ToTimeLimitedDataProtector();

    public string Create(int userId, SavedPlaylistGame[] games) =>
        protector.Protect(JsonSerializer.Serialize(new Ticket(userId,
            games.Select(game => game with { PriceCents = null }).ToArray())), TimeSpan.FromHours(8));

    public SavedPlaylistGame[]? Read(string? token, int userId)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 80_000)
            return null;

        try
        {
            var ticket = JsonSerializer.Deserialize<Ticket>(protector.Unprotect(token));
            return ticket?.UserId == userId ? ticket.Games : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private sealed record Ticket(int UserId, SavedPlaylistGame[] Games);
}
