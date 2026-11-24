namespace Gamify.Api.Data;

public sealed class SavedPlaylist
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string GamesJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record SavedPlaylistGame(
    int Id,
    string Title,
    string CoverImageUrl,
    int PriceCents,
    string[] Platforms,
    int MatchPercentage,
    string Explanation,
    int MaxPlayers,
    int? Pegi,
    string? Esrb);
