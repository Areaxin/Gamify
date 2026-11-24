using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gamify.Api.Ai;

public sealed class OpenAiResponsesClient(HttpClient httpClient, IOptions<AiOptions> options)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey)
        && !string.IsNullOrWhiteSpace(options.Value.Model);

    public Task<string> GetTextAsync(string input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return SendTextAsync(new { model = options.Value.Model, input, store = false }, cancellationToken);
    }

    public Task<string> GetStructuredTextAsync(
        string input, string instructions, string schemaName, JsonElement schema, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return SendTextAsync(new
        {
            model = options.Value.Model,
            input,
            instructions,
            store = false,
            text = new { format = new { type = "json_schema", name = schemaName, strict = true, schema } }
        }, cancellationToken);
    }

    internal async Task<WebSearchResponse> SearchAsync(
        string input, string instructions, JsonElement schema, CancellationToken cancellationToken)
    {
        using var json = await SendAsync(new
        {
            model = options.Value.Model,
            input,
            instructions,
            store = false,
            reasoning = new { effort = "low" },
            max_tool_calls = 6,
            tools = new[] { new { type = "web_search", external_web_access = true,
                user_location = new { type = "approximate", country = "LT", timezone = "Europe/Vilnius" } } },
            tool_choice = "required",
            include = new[] { "web_search_call.action.sources" },
            text = new { format = new { type = "json_schema", name = "game_search", strict = true, schema } }
        }, cancellationToken, minimumTimeoutSeconds: 75);

        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in json.RootElement.GetProperty("output").EnumerateArray())
        {
            if (!item.TryGetProperty("action", out var action)) continue;
            if (action.TryGetProperty("url", out var openedUrl) && openedUrl.GetString() is { } opened)
                sources.Add(opened);
            if (action.TryGetProperty("sources", out var entries))
            {
                foreach (var source in entries.EnumerateArray())
                    if (source.TryGetProperty("url", out var url) && url.GetString() is { } address)
                        sources.Add(address);
            }
        }
        foreach (var part in TextParts(json))
        {
            if (!part.TryGetProperty("annotations", out var annotations)) continue;
            foreach (var annotation in annotations.EnumerateArray())
                if (annotation.TryGetProperty("url", out var url) && url.GetString() is { } address)
                    sources.Add(address);
        }
        if (!json.RootElement.GetProperty("output").EnumerateArray().Any(item =>
                item.GetProperty("type").GetString() == "web_search_call"))
            throw new InvalidOperationException("OpenAI did not run web search.");

        return new WebSearchResponse(ReadText(json), sources);
    }

    private async Task<string> SendTextAsync<T>(T payload, CancellationToken cancellationToken)
    {
        using var json = await SendAsync(payload, cancellationToken);
        return ReadText(json);
    }

    private async Task<JsonDocument> SendAsync<T>(
        T payload, CancellationToken cancellationToken, int minimumTimeoutSeconds = 0)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Set OPENAI_API_KEY and OPENAI_MODEL to use the OpenAI client.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        request.Content = JsonContent.Create(payload);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(options.Value.TimeoutSeconds, minimumTimeoutSeconds)));
        using var response = await httpClient.SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
        var json = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);
        if (json.RootElement.TryGetProperty("status", out var status) && status.GetString() != "completed")
        {
            json.Dispose();
            throw new InvalidOperationException("OpenAI did not complete the response.");
        }

        return json;
    }

    private static IEnumerable<JsonElement> TextParts(JsonDocument json) =>
        json.RootElement.GetProperty("output").EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "message")
            .SelectMany(item => item.GetProperty("content").EnumerateArray())
            .Where(part => part.GetProperty("type").GetString() == "output_text");

    private static string ReadText(JsonDocument json)
    {
        var text = string.Concat(TextParts(json).Select(part => part.GetProperty("text").GetString()));
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidOperationException("OpenAI returned no text output.");
    }
}

internal sealed record WebSearchResponse(string Text, HashSet<string> Sources);
