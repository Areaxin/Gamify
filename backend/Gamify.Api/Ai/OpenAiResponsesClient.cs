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
        return SendAsync(new { model = options.Value.Model, input, store = false }, cancellationToken);
    }

    public Task<string> GetStructuredTextAsync(
        string input, string instructions, string schemaName, JsonElement schema, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return SendAsync(new
        {
            model = options.Value.Model,
            input,
            instructions,
            store = false,
            text = new { format = new { type = "json_schema", name = schemaName, strict = true, schema } }
        }, cancellationToken);
    }

    private async Task<string> SendAsync<T>(T payload, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Set OPENAI_API_KEY and OPENAI_MODEL to use the OpenAI client.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        if (json.RootElement.TryGetProperty("status", out var status) && status.GetString() != "completed")
            throw new InvalidOperationException("OpenAI did not complete the response.");


        var textParts = json.RootElement.GetProperty("output").EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "message")
            .SelectMany(item => item.GetProperty("content").EnumerateArray())
            .Where(part => part.GetProperty("type").GetString() == "output_text")
            .Select(part => part.GetProperty("text").GetString());

        var text = string.Concat(textParts);
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidOperationException("OpenAI returned no text output.");
    }
}
