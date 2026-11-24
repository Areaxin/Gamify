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

        if (budgetCents is { } budget && game.PriceCents is { } price)
        {
            var budgetScore = budget == 0
                ? price == 0 ? 100 : 0
                : (int)Math.Clamp(Math.Round((budget - price) * 100m / budget,
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
        GameMood.Horror => "siaubo",
        _ => mood.ToString()
    };
}
