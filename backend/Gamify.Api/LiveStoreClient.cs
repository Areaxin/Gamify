using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gamify.Api;

public sealed class LiveStoreClient(HttpClient http)
{
    private static readonly Regex JsonLd = new(
        """<script\b[^>]*\btype\s*=\s*["']application/ld\+json["'][^>]*>(.*?)</script\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    public async Task<StoreGameDetails?> CheckAsync(StoreLink store, CancellationToken cancellationToken)
    {
        try
        {
            if (store.SteamAppId is { } appId)
            {
                using var response = await http.GetAsync(
                    $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=lt&l=english", cancellationToken);
                if (!response.IsSuccessStatusCode) return null;
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (!json.RootElement.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out var item) ||
                    !item.GetProperty("success").GetBoolean() || !item.TryGetProperty("data", out var game) ||
                    game.GetProperty("type").GetString() != "game")
                    return null;

                var price = game.GetProperty("is_free").GetBoolean() ? 0 : SteamPrice(game);
                var categories = game.TryGetProperty("categories", out var categoryList)
                    ? categoryList.EnumerateArray().Select(category => category.GetProperty("id").GetInt32()).ToArray()
                    : null;
                var multiplayer = categories?.Any(id => id is 1 or 9 or 24 or 27 or 36 or 37 or 38 or 39 or 44 or 49);
                int.TryParse(game.GetProperty("required_age").ToString(), out var requiredAge);
                var hasPcRequirements = game.TryGetProperty("pc_requirements", out var requirements)
                    && requirements.ValueKind == JsonValueKind.Object
                    && requirements.TryGetProperty("minimum", out var minimum)
                    && !string.IsNullOrWhiteSpace(minimum.GetString());
                return new StoreGameDetails(game.GetProperty("name").GetString(), price, multiplayer, requiredAge,
                    $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
                    hasPcRequirements, categories?.Contains(2));
            }

            using var page = await http.GetAsync(store.Url, cancellationToken);
            if (!page.IsSuccessStatusCode)
                return null;

            var html = await page.Content.ReadAsStringAsync(cancellationToken);
            int? productPrice = null;
            string? cover = null;
            foreach (Match script in JsonLd.Matches(html))
            {
                try
                {
                    using var json = JsonDocument.Parse(script.Groups[1].Value);
                    productPrice ??= ProductPrice(json.RootElement);
                    if (store.Platform == "PlayStation")
                        cover ??= PlayStationCover(json.RootElement);
                }
                catch (JsonException) { }
            }
            return new StoreGameDetails(null, productPrice, null, 0, cover);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
            error is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException
                or InvalidOperationException or RegexMatchTimeoutException or FormatException)
        {
            return null;
        }
    }

    private static int? SteamPrice(JsonElement game) =>
        game.TryGetProperty("price_overview", out var price) &&
        price.GetProperty("currency").GetString() == "EUR" &&
        price.GetProperty("final").TryGetInt32(out var amount) && amount >= 0 ? amount : null;

    private static int? ProductPrice(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            var products = node.EnumerateArray().Select(ProductPrice).OfType<int>().Distinct().Take(2).ToArray();
            return products.Length == 1 ? products[0] : null;
        }
        if (node.ValueKind != JsonValueKind.Object)
            return null;
        if (node.TryGetProperty("@graph", out var graph)) return ProductPrice(graph);
        if (!IsProduct(node) || !node.TryGetProperty("offers", out var offers))
            return null;
        if (offers.ValueKind != JsonValueKind.Array) return OfferPrice(offers);

        var prices = offers.EnumerateArray().Select(OfferPrice).OfType<int>().Distinct().Take(2).ToArray();
        return prices.Length == 1 ? prices[0] : null;
    }

    private static string? PlayStationCover(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array)
            return node.EnumerateArray().Select(PlayStationCover).FirstOrDefault(cover => cover is not null);
        if (node.ValueKind != JsonValueKind.Object)
            return null;
        if (node.TryGetProperty("@graph", out var graph))
            return PlayStationCover(graph);
        if (!IsProduct(node) || !node.TryGetProperty("image", out var image) || image.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(image.GetString(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.IdnHost != "image.api.playstation.com")
            return null;

        return uri.AbsoluteUri;
    }

    private static bool IsProduct(JsonElement node) =>
        node.TryGetProperty("@type", out var type) && type.ValueKind == JsonValueKind.String &&
        type.GetString() is "Product" or "VideoGame" or "SoftwareApplication";

    private static int? OfferPrice(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return null;
        if (!node.TryGetProperty("@type", out var type) || type.ValueKind != JsonValueKind.String ||
            type.GetString() != "Offer" ||
            !node.TryGetProperty("priceCurrency", out var currency) || currency.GetString() != "EUR" ||
            !node.TryGetProperty("price", out var priceField) ||
            !decimal.TryParse(priceField.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var euros) || euros < 0 || euros > int.MaxValue / 100m)
            return null;

        return (int)Math.Round(euros * 100m, MidpointRounding.AwayFromZero);
    }
}

public sealed record StoreGameDetails(
    string? Title, int? PriceCents, bool? Multiplayer, int MinimumAge, string? CoverImageUrl,
    bool HasPcRequirements = false, bool? SinglePlayer = null);
