using Gamify.Api;
using Gamify.Api.Data;
using Xunit;

namespace Gamify.Api.Tests;

public sealed class RecommendationScoringTests
{
    [Fact]
    public void Calculate_UsesWeightedPreferenceScoresAndRoundsAwayFromZero()
    {
        var game = new Game
        {
            Moods = GameMood.Relaxing,
            PriceCents = 1000,
            MaxPlayers = 4,
            Difficulty = 2
        };

        var match = RecommendationScoring.Calculate(game, "relaxing", 2000, 2, "beginner");

        Assert.Equal(78, match.Percentage);
        Assert.Contains("Atitinka", match.Explanation);
        Assert.Contains("50/100", match.Explanation);
    }

    [Fact]
    public void Calculate_UsesNeutralScoreWhenNoPreferencesWereExtracted()
    {
        var match = RecommendationScoring.Calculate(new Game(), null, null, null, null);

        Assert.Equal(50, match.Percentage);
        Assert.False(string.IsNullOrWhiteSpace(match.Explanation));
    }

    [Fact]
    public void Calculate_ReturnsZeroWhenTheOnlyPreferenceDoesNotMatch()
    {
        var match = RecommendationScoring.Calculate(new Game { Moods = GameMood.Relaxing }, "action", null, null, null);

        Assert.Equal(0, match.Percentage);
        Assert.Contains("Neatitinka", match.Explanation);
    }
}
