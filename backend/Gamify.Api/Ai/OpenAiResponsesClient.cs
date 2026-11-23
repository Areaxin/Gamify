using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gamify.Api.Ai;

public sealed class OpenAiResponsesClient(HttpClient httpClient, IOptions<AiOptions> options)
{
    public async Task<string> GetTextAsync(string input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new InvalidOperationException("Set OPENAI_API_KEY and OPENAI_MODEL to use the OpenAI client.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new { model = settings.Model, input, store = false });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

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
