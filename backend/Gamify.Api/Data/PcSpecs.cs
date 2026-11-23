namespace Gamify.Api.Data;

public static class PcSpecs
{
    public static readonly string[] Levels = ["Žemas", "Vidutinis", "Aukštas"];

    public static int Rank(string? level) => Array.IndexOf(Levels, level) + 1;
}
