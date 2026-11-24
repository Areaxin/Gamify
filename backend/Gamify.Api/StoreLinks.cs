namespace Gamify.Api;

public sealed record StoreLink(string Url, string Store);

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
            ["store.epicgames.com"] = "Epic Games"
        };

    public static StoreLink? Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            return null;

        return AllowedHosts.TryGetValue(uri.IdnHost, out var store) ? new StoreLink(uri.AbsoluteUri, store) : null;
    }

    public static string Steam(int appId) => $"https://store.steampowered.com/app/{appId}/";

    public static string Epic(string slug) => $"https://store.epicgames.com/p/{slug}";
}
