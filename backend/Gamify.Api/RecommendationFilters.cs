using Gamify.Api.Data;

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

public sealed record GroupMemberConstraint(
    string Name, int? MaxBudgetCents, ExperienceLevel? Experience, string[] Platforms);

public sealed record GroupConstraintBlocker(string Name, string[] Constraints);

public sealed record GroupRecommendationProfile(
    HashSet<int> SharedPlatformIds, int PcLevel, int Age, int GroupSize, GroupMemberConstraint[] Participants)
{
    public static GroupRecommendationProfile FromMembers(
        IEnumerable<User> users, int requesterUserId, DateOnly today)
    {
        var members = users.ToArray();
        if (members.Length == 0 || members.Any(member => member.BirthDate is null))
            throw new ArgumentException("All group members must have a saved birth date.", nameof(users));

        var sharedPlatformIds = members[0].Platforms.Select(platform => platform.Id).ToHashSet();
        foreach (var member in members.Skip(1))
            sharedPlatformIds.IntersectWith(member.Platforms.Select(platform => platform.Id));

        var age = AgeRatings.AgeOn(members.Max(member => member.BirthDate!.Value), today);
        var pcLevel = members.Where(member => member.Platforms.Any(platform => platform.Id == Platform.PcId))
            .Select(member => PcSpecs.Rank(member.PcLevel)).DefaultIfEmpty(0).Min();
        var participants = members.Select(member => new GroupMemberConstraint(
            member.Id == requesterUserId ? "Jūs" : member.Name,
            member.PreferredBudgetCents,
            ParseExperience(member.PreferredExperience),
            member.Platforms.Select(platform => platform.Name).ToArray())).ToArray();

        return new(sharedPlatformIds, pcLevel, age, members.Length, participants);
    }

    private static ExperienceLevel? ParseExperience(string? value) =>
        Enum.TryParse<ExperienceLevel>(value, true, out var level) && Enum.IsDefined(level)
            ? level
            : null;
}

public static class GroupConstraintEvaluator
{
    public static GroupConstraintBlocker[] FindBlockers(
        string platform,
        int? priceCents,
        int difficulty,
        int? requesterBudgetCents,
        ExperienceLevel? requesterExperience,
        IEnumerable<GroupMemberConstraint> participants)
    {
        var blockers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var participant in participants)
        {
            if (!participant.Platforms.Contains(platform, StringComparer.Ordinal))
                Add(participant.Name, "platformos");
            if (participant.MaxBudgetCents is { } memberBudget &&
                (priceCents is not { } memberPrice || memberPrice > memberBudget))
                Add(participant.Name, "biudžetas");
            if (participant.Experience is { } memberExperience && difficulty > (int)memberExperience)
                Add(participant.Name, "patirtis");
        }

        if (requesterBudgetCents is { } maxBudget &&
            (priceCents is not { } price || price > maxBudget))
            Add("Jūs", "biudžetas");
        if (requesterExperience is { } experience && difficulty > (int)experience)
            Add("Jūs", "patirtis");

        return blockers.Select(blocker =>
            new GroupConstraintBlocker(blocker.Key, blocker.Value.Order(StringComparer.Ordinal).ToArray())).ToArray();

        void Add(string name, string constraint)
        {
            if (!blockers.TryGetValue(name, out var constraints))
            {
                constraints = new HashSet<string>(StringComparer.Ordinal);
                blockers.Add(name, constraints);
            }
            constraints.Add(constraint);
        }
    }
}
