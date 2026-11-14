using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SportsCenterManagement.BLL.Services;

public sealed record AiTurn(string Role, string Text, DateTime? CreatedAt = null);

public sealed class GeminiService(HttpClient client, IConfiguration configuration)
{
    public const string ModelName = "gemini-3.5-flash-lite";
    private const int MaxTransientAttempts = 2;
    private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromMilliseconds(800);

    /// <summary>Model can be overridden with <c>AI:Model</c> without a rebuild.</summary>
    public string Model => string.IsNullOrWhiteSpace(configuration["AI:Model"]) ? ModelName : configuration["AI:Model"]!.Trim();

    public async Task<string> GenerateAsync(string instruction, IEnumerable<AiTurn> history, bool json, CancellationToken ct,
        double? temperature = null, int maxOutputTokens = 2048)
    {
        var apiKey = configuration["AI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw Fail("AI chưa được cấu hình. Hãy thêm Gemini API key ở backend.", 503);
        var contents = history.Select(t => new { role = t.Role, parts = new[] { new { text = t.Text } } }).ToArray();

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");
            request.Headers.Add("x-goog-api-key", apiKey.Trim());
            request.Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = instruction } } },
                contents,
                generationConfig = new
                {
                    maxOutputTokens,
                    temperature = temperature ?? (json ? 0.2 : 0.6),
                    responseMimeType = json ? "application/json" : "text/plain"
                }
            });
            try
            {
                using var response = await client.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw Fail("Đã hết hạn mức Gemini tạm thời. Vui lòng thử lại sau.", 429);
                if (IsTransient(response.StatusCode) && attempt < MaxTransientAttempts)
                {
                    await Task.Delay(TransientRetryDelay, ct);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw Fail("Dịch vụ AI chưa sẵn sàng. Kiểm tra API key, model và hạn mức ở Google AI Studio.", 503);
                using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                return ReadText(payload.RootElement, json);
            }
            catch (HttpRequestException) when (attempt < MaxTransientAttempts) { await Task.Delay(TransientRetryDelay, ct); }
            catch (HttpRequestException) { throw Fail("Không kết nối được Gemini. Vui lòng thử lại.", 503); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Fail("Gemini phản hồi quá lâu. Vui lòng thử lại.", 504); }
            catch (JsonException) { throw Fail("Phản hồi từ Gemini không hợp lệ.", 502); }
        }
    }

    /// <summary>Extracts candidate text and turns blocked or truncated generations into tagged, user-safe errors.</summary>
    internal static string ReadText(JsonElement root, bool json)
    {
        if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _))
            throw Fail("Câu hỏi bị bộ lọc an toàn của AI từ chối. Hãy diễn đạt lại.", 422);
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            throw Fail("AI không trả nội dung cho yêu cầu này. Hãy thử câu hỏi khác.", 502);
        var candidate = candidates[0];
        var finishReason = candidate.TryGetProperty("finishReason", out var reason) ? reason.GetString() : null;
        if (finishReason is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII")
            throw Fail("Nội dung bị bộ lọc an toàn của AI chặn. Hãy diễn đạt lại.", 422);
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
            throw Fail("AI không trả nội dung cho yêu cầu này. Hãy thử câu hỏi khác.", 502);
        // Thought-summary parts (thinking models) are internal reasoning and must never reach users.
        var text = string.Concat(parts.EnumerateArray()
            .Where(p => !(p.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
            .Where(p => p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
            .Select(p => p.GetProperty("text").GetString()));
        if (string.IsNullOrWhiteSpace(text)) throw Fail("AI trả nội dung rỗng.", 502);
        // A truncated JSON document cannot be parsed safely; plain text is still useful to the member.
        if (json && finishReason == "MAX_TOKENS") throw Fail("Phản hồi AI bị cắt ngắn.", 502);
        return text.Trim();
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static InvalidOperationException Fail(string message, int statusCode) =>
        new(message) { Data = { ["StatusCode"] = statusCode } };
}
