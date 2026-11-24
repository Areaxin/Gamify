using System.Net;
using Gamify.Api.Data;

namespace Gamify.Api;

public sealed record RecommendationMatch(int Percentage, string Explanation);

public static class RecommendationScoring
{
    public static RecommendationMatch Calculate(
        Game game,
        string? mood,
        int? budgetCents,
        int? playerCount,
        string? experience)
    {
        var factors = new List<(int Weight, int Score)>();
        var reasons = new List<string>();

        if (Enum.TryParse<GameMood>(mood, ignoreCase: true, out var requestedMood))
        {
            var matchesMood = (game.Moods & requestedMood) != 0;
            factors.Add((40, matchesMood ? 100 : 0));
            reasons.Add(matchesMood
                ? $"atitinka „{MoodName(requestedMood)}“ nuotaiką"
                : $"neatitinka „{MoodName(requestedMood)}“ nuotaikos");
        }

        if (budgetCents is { } budget)
        {
            var budgetScore = budget == 0
                ? game.PriceCents == 0 ? 100 : 0
                : (int)Math.Clamp(Math.Round((budget - game.PriceCents) * 100m / budget,
                    MidpointRounding.AwayFromZero), 0, 100);
            factors.Add((25, budgetScore));
            reasons.Add($"kaina sudaro {budgetScore}/100 biudžeto balų");
        }

        if (playerCount is { } players)
        {
            var supportsPlayers = game.MaxPlayers >= players;
            factors.Add((15, supportsPlayers ? 100 : 0));
            reasons.Add(supportsPlayers
                ? $"palaiko {players} žaidėjų"
                : $"nepalaiko {players} žaidėjų");
        }

        if (ExperienceTarget(experience) is { } target)
        {
            var difficultyScore = Math.Max(0, 100 - Math.Abs(game.Difficulty - target) * 50);
            factors.Add((20, difficultyScore));
            reasons.Add($"sudėtingumas {game.Difficulty}/3, pageidaujamas lygis „{ExperienceName(experience!)}“");
        }

        var percentage = factors.Count == 0
            ? 50
            : (int)Math.Round(factors.Sum(factor => factor.Weight * factor.Score) /
                (double)factors.Sum(factor => factor.Weight), MidpointRounding.AwayFromZero);
        var explanation = reasons.Count == 0
            ? "Užklausoje nėra aiškių pageidavimų, todėl atitikmuo vertinamas neutraliai."
            : char.ToUpperInvariant(reasons[0][0]) + reasons[0][1..] +
              (reasons.Count > 1 ? "; " + string.Join("; ", reasons.Skip(1)) : string.Empty) + ".";

        return new RecommendationMatch(percentage, explanation);
    }

    private static int? ExperienceTarget(string? experience) => experience?.ToLowerInvariant() switch
    {
        "beginner" => 1,
        "intermediate" => 2,
        "experienced" => 3,
        _ => null
    };

    private static string ExperienceName(string experience) => experience.ToLowerInvariant() switch
    {
        "beginner" => "pradedančiajam",
        "intermediate" => "vidutinės patirties žaidėjui",
        "experienced" => "patyrusiam žaidėjui",
        _ => experience
    };

    private static string MoodName(GameMood mood) => mood switch
    {
        GameMood.Relaxing => "rami",
        GameMood.Adventure => "nuotykių",
        GameMood.Action => "veiksmo",
        GameMood.Competitive => "varžybinė",
        GameMood.Puzzle => "galvosūkių",
        GameMood.Strategy => "strateginė",
        GameMood.Party => "vakarėlio",
        _ => mood.ToString()
    };
}

internal static class GameCoverImages
{
    private const string SteamImageBase = "https://cdn.akamai.steamstatic.com/steam/apps";

    private static readonly IReadOnlyDictionary<int, int> SteamAppIds = new Dictionary<int, int>
    {
        [1] = 413150,
        [2] = 673950,
        [3] = 477160,
        [4] = 837470,
        [5] = 914710,
        [6] = 2707930,
        [7] = 2230110,
        [8] = 1135690,
        [11] = 1672970,
        [12] = 252950,
        [13] = 620,
        [14] = 2209710,
        [15] = 550,
        [16] = 379720
    };

    public static string For(int gameId, string title)
    {
        if (SteamAppIds.TryGetValue(gameId, out var appId))
            return $"{SteamImageBase}/{appId}/library_600x900.jpg";

        var safeTitle = WebUtility.HtmlEncode(title);
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 600">
              <defs><linearGradient id="g" x2="1" y2="1"><stop stop-color="#4049aa"/><stop offset="1" stop-color="#202838"/></linearGradient></defs>
              <rect width="400" height="600" fill="url(#g)"/>
              <circle cx="200" cy="210" r="95" fill="#ffffff" fill-opacity=".12"/>
              <path d="M140 210h120m-60-60v120" stroke="#fff" stroke-opacity=".7" stroke-width="12"/>
              <text x="200" y="390" fill="#fff" font-family="sans-serif" font-size="30" text-anchor="middle">{safeTitle}</text>
              <text x="200" y="435" fill="#dce2ed" font-family="sans-serif" font-size="16" text-anchor="middle">GAMIFY REKOMENDACIJA</text>
            </svg>
            """;

        return "data:image/svg+xml," + Uri.EscapeDataString(svg);
    }
}
