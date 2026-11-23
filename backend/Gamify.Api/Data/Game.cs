namespace Gamify.Api.Data;

public sealed class Game
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int PriceCents { get; set; }
    public int MaxPlayers { get; set; }
    public GameMood Moods { get; set; }
    public int Difficulty { get; set; }
    public int? MinimumPcLevel { get; set; }
    public int? PegiAge { get; set; }
    public int? EsrbAge { get; set; }
    public List<Platform> Platforms { get; set; } = [];
}

[Flags]
public enum GameMood
{
    Relaxing = 1,
    Adventure = 2,
    Action = 4,
    Competitive = 8,
    Puzzle = 16,
    Strategy = 32,
    Party = 64
}
