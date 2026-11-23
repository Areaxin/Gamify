namespace Gamify.Api.Ai;

public sealed class AiOptions
{
    public string? ApiKey { get; init; }
    public string? Model { get; init; }
    public int TimeoutSeconds { get; init; } = 30;

    internal static AiOptions Load(string contentRoot)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var envPath = Path.Combine(contentRoot, ".env");
        if (File.Exists(envPath))
        {
            foreach (var rawLine in File.ReadLines(envPath))
            {
                var line = rawLine.Trim();
                var separator = line.IndexOf('=');
                if (line.StartsWith('#') || separator <= 0)
                {
                    continue;
                }

                var value = line[(separator + 1)..].Trim();
                if (value.Length >= 2 && value[0] is ('"' or '\'') && value[^1] == value[0])
                {
                    value = value[1..^1];
                }

                values[line[..separator].Trim()] = value;
            }
        }

        string? Read(string key) =>
            (Environment.GetEnvironmentVariable(key) ?? values.GetValueOrDefault(key))?.Trim();

        var timeout = Read("OPENAI_TIMEOUT_SECONDS");
        return new AiOptions
        {
            ApiKey = Read("OPENAI_API_KEY"),
            Model = Read("OPENAI_MODEL"),
            TimeoutSeconds = int.TryParse(timeout, out var seconds) && seconds is > 0 and <= int.MaxValue / 1000
                ? seconds
                : 30
        };
    }
}
