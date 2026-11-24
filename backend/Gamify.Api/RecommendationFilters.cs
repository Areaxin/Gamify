namespace Gamify.Api;

/// <summary>GAM-US13: patirties lygiai, kuriuos galima pasirinkti filtru.</summary>
public enum ExperienceLevel
{
    Beginner = 1,
    Intermediate = 2,
    Experienced = 3
}

/// <summary>
/// GAM-US13: aiškūs paieškos filtrai. Visi laukai nebūtini; nurodytas filtras laimi prieš tą pačią reikšmę,
/// išgautą iš laisvo teksto.
/// </summary>
/// <param name="MaxPriceEur">Didžiausia kaina eurais (0 – tik nemokami).</param>
/// <param name="PlayerCount">Žaidėjų skaičius, 1–8.</param>
/// <param name="PlatformIds">Platformos, kuriose ieškoti (bet kurios). Tuščia arba null – visos profilio platformos.</param>
/// <param name="Experience">Vienas iš <see cref="ExperienceLevel"/> pavadinimų.</param>
public sealed record RecommendationFilters(decimal? MaxPriceEur, int? PlayerCount, int[]? PlatformIds, string? Experience)
{
    public const decimal MaxPriceLimitEur = 1000m;
    public const int MinPlayers = 1;
    public const int MaxPlayers = 8;

    /// <summary>Grąžina klaidos pranešimą arba null, jei reikšmės teisingos.</summary>
    public string? Validate()
    {
        if (MaxPriceEur is { } price)
        {
            if (price < 0)
                return "Didžiausia kaina negali būti neigiama.";
            if (price > MaxPriceLimitEur)
                return $"Didžiausia kaina negali viršyti {MaxPriceLimitEur:0} €.";
            if (decimal.Round(price, 2) != price)
                return "Kainą nurodykite ne daugiau kaip dviejų skaitmenų po kablelio tikslumu.";
        }
        if (PlayerCount is { } players && players is < MinPlayers or > MaxPlayers)
            return $"Žaidėjų skaičius turi būti nuo {MinPlayers} iki {MaxPlayers}.";
        if (PlatformIds?.Any(id => id <= 0) == true)
            return "Pasirinkta nežinoma platforma.";
        if (!string.IsNullOrWhiteSpace(Experience) && ExperienceLevelValue is null)
            return "Pasirinktas nežinomas patirties lygis.";

        return null;
    }

    public int? MaxBudgetCents => MaxPriceEur is { } price ? (int)(price * 100) : null;

    public ExperienceLevel? ExperienceLevelValue =>
        Enum.TryParse<ExperienceLevel>(Experience?.Trim(), ignoreCase: true, out var level) &&
        Enum.IsDefined(level) && !int.TryParse(Experience, out _)
            ? level
            : null;
}

/// <summary>Filtrų reikšmės, perduodamos paieškai; null reiškia „naudoti iš teksto išgautą reikšmę“.</summary>
public sealed record FilterOverrides(int? MaxBudgetCents, int? PlayerCount, ExperienceLevel? Experience)
{
    /// <summary>Pavadinimas, kurį supranta <see cref="RecommendationScoring"/> ir DI schema.</summary>
    public string? ExperienceName => Experience?.ToString().ToLowerInvariant();
}
