namespace Gamify.Api;

public sealed record StoreLink(string Url, string Store, string Platform, int? SteamAppId = null);

/// <summary>
/// GAM-US12: parduotuvių nuorodų baltasis sąrašas. Nuoroda grąžinama naudotojui tik tada, kai ji yra HTTPS,
/// be naudotojo duomenų ir nestandartinio prievado, o domenas tiksliai sutampa su leidžiamu (be subdomenų).
/// </summary>
public static class StoreLinks
{
    public static readonly IReadOnlyDictionary<string, string> AllowedHosts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["store.steampowered.com"] = "Steam",
            ["store.epicgames.com"] = "Epic Games",
            ["store.playstation.com"] = "PlayStation Store",
            ["www.xbox.com"] = "Xbox Store",
            ["www.microsoft.com"] = "Xbox Store",
            ["www.nintendo.com"] = "Nintendo eShop"
        };

    public static StoreLink? Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            return null;

        if (!AllowedHosts.TryGetValue(uri.IdnHost, out var store))
            return null;

        var path = uri.AbsolutePath;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var canonical = $"{uri.Scheme}://{uri.IdnHost}{path.TrimEnd('/')}";
        switch (store)
        {
            case "Steam" when parts.Length >= 2 && parts[0] == "app" &&
                int.TryParse(parts[1], out var id) && id > 0:
                return new StoreLink(Steam(id), store, "PC", id);
            case "Epic Games" when parts.Length >= 2 && parts[^2] == "p":
                return new StoreLink(parts.Length == 2
                    ? $"https://store.epicgames.com/en-US/p/{parts[^1]}"
                    : canonical, store, "PC");
            case "PlayStation Store" when parts.Length >= 2 && parts[^2] is "product" or "concept":
                return new StoreLink(canonical, store, "PlayStation");
            case "Xbox Store" when path.Contains("/store/", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4:
                return new StoreLink(canonical, store, "Xbox");
            case "Nintendo eShop" when path.Contains("/products/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Games/", StringComparison.OrdinalIgnoreCase):
                return new StoreLink(canonical, store, "Switch");
            default:
                return null;
        }
    }

    public static string Steam(int appId) => $"https://store.steampowered.com/app/{appId}/";

    public static string Epic(string slug) => $"https://store.epicgames.com/p/{slug}";
}
