namespace Gamify.Api;

public sealed record AgeRatings(int? Pegi, string Esrb)
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static bool IsValid(DateOnly birthDate, DateOnly today) =>
        birthDate <= today && birthDate >= today.AddYears(-120);

    public static int AgeOn(DateOnly birthDate, DateOnly today)
    {
        var years = today.Year - birthDate.Year;
        return birthDate.AddYears(years) > today ? years - 1 : years;
    }

    public static AgeRatings ForAge(int age) =>
        new(age switch { >= 18 => 18, >= 16 => 16, >= 12 => 12, >= 7 => 7, >= 3 => 3, _ => null },
            EsrbLabel(age switch { >= 18 => 18, >= 17 => 17, >= 13 => 13, >= 10 => 10, _ => 0 })!);

    public static string? EsrbLabel(int? minimumAge) => minimumAge switch
    {
        0 => "E",
        10 => "E10+",
        13 => "T",
        17 => "M",
        18 => "AO",
        _ => null
    };
}
