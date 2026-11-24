using System.Text.Json;
using Gamify.Api.Ai;
using Gamify.Api.Data;

namespace Gamify.Api;

public sealed class LiveRecommendations(OpenAiResponsesClient ai, LiveStoreClient stores, ILogger<LiveRecommendations> logger)
{
    public async Task<SavedPlaylistGame[]> SearchAsync(
        string text, string[] platforms, int pcLevel, int age, int groupSize, CancellationToken cancellationToken)
    {
        var ratings = AgeRatings.ForAge(age);
        var input = JsonSerializer.Serialize(new
        {
            query = text, platforms, pcLevel, maxPegi = ratings.Pegi, maxEsrb = ratings.Esrb,
            requireAgeRating = age < 18, minimumPlayers = groupSize
        });
        var response = await ai.SearchAsync(input, WebGameSearch.Instructions, WebGameSearch.Schema, cancellationToken);
        var result = WebGameSearch.Parse(response.Text);
        var sources = response.Sources.Select(SourceKey).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var products = response.Sources.Select(StoreLinks.Resolve).OfType<StoreLink>()
            .Select(store => store.Url).ToHashSet(StringComparer.Ordinal);

        var players = Math.Max(result.Filters.PlayerCount ?? 1, groupSize);
        var budget = result.Filters.MaxBudgetCents;
        var pending = result.Games.Select(KeepConfirmedRatings)
            .Select(game => (Game: game, Store: StoreLinks.Resolve(game.StoreUrl)))
            .Where(candidate => candidate.Store is { } store && (store.SteamAppId is not null || products.Contains(store.Url))
                && platforms.Contains(store.Platform) && gameFits(candidate.Game, store))
            .DistinctBy(candidate => candidate.Store!.Url).ToArray();

        using var checks = new SemaphoreSlim(4);
        var verified = await Task.WhenAll(pending.Select(async candidate =>
        {
            var (game, store) = (candidate.Game, candidate.Store!);
            await checks.WaitAsync(cancellationToken);
            StoreGameDetails? details;
            try { details = await stores.CheckAsync(store, cancellationToken); }
            finally { checks.Release(); }
            if (details is null || details.MinimumAge > age || players > 1 && details.Multiplayer == false ||
                result.Filters.PlayerCount == 1 && players == 1 && details.SinglePlayer == false ||
                store.Platform == "PC" && game.MinimumPcLevel is null && !details.HasPcRequirements ||
                budget is { } max && (details.PriceCents is not { } price || price > max))
            {
                logger.LogDebug("Store validation skipped {StoreUrl}. Store responded: {Available}; has price: {HasPrice}; multiplayer: {Multiplayer}.",
                    store.Url, details is not null, details?.PriceCents is not null, details?.Multiplayer);
                return null;
            }

            var match = RecommendationScoring.Calculate(
                new Game { PriceCents = details.PriceCents, MaxPlayers = game.MaxPlayers, Difficulty = game.Difficulty, Moods = game.Moods },
                result.Filters.Mood, budget, result.Filters.PlayerCount is not null || groupSize > 1 ? players : null,
                result.Filters.Experience);

            return new SavedPlaylistGame(0, details.Title ?? game.Title, details.CoverImageUrl, details.PriceCents,
                [store.Platform], match.Percentage, match.Explanation, game.MaxPlayers, game.Pegi, AgeRatings.EsrbLabel(game.Esrb),
                store.Url, store.Store, game.RatingUrl, store.Platform == "PC" ? game.MinimumPcLevel ?? 3 : null);
        }));

        var recommendations = verified.OfType<SavedPlaylistGame>()
            .OrderByDescending(game => game.MatchPercentage).ThenBy(game => game.PriceCents ?? int.MaxValue)
            .ThenBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(game => game.Title, StringComparer.OrdinalIgnoreCase).Take(10)
            .Select((game, index) => game with { Id = index + 1 }).ToArray();
        logger.LogInformation("Web search proposed {CandidateCount} games, {CheckedCount} reached storefront checks, and {ResultCount} matched.",
            result.Games.Length, pending.Length, recommendations.Length);
        return recommendations;

        bool gameFits(WebGame game, StoreLink store)
        {
            if (age < 18 && game.Pegi is null && game.Esrb is null ||
                game.Pegi > age || game.Esrb > age || game.MaxPlayers < players)
                return false;
            if (store.Platform == "PC" && (game.MinimumPcLevel ?? 3) > pcLevel)
                return false;

            return !Enum.TryParse<GameMood>(result.Filters.Mood, true, out var mood) || (game.Moods & mood) != 0;
        }

        WebGame KeepConfirmedRatings(WebGame game)
        {
            if (game.RatingUrl is { } url && SourceKey(url) is { } key && sources.Contains(key) &&
                IsRatingSource(url, StoreLinks.Resolve(game.StoreUrl)))
                return game;

            return game with { Pegi = null, Esrb = null, RatingUrl = null };
        }
    }

    private static bool IsRatingSource(string url, StoreLink? store)
    {
        var uri = new Uri(url);
        return uri.IdnHost is "pegi.info" or "www.pegi.info" or "esrb.org" or "www.esrb.org" ||
            store is not null && StoreLinks.Resolve(url)?.Url == store.Url;
    }

    private static string? SourceKey(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort && uri.UserInfo.Length == 0
            ? StoreLinks.Resolve(url)?.Url ?? (uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + uri.Query)
            : null;
}
