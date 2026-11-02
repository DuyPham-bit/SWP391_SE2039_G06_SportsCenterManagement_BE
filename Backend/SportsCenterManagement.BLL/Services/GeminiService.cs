using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SportsCenterManagement.BLL.Services;

public sealed record AiTurn(string Role, string Text, DateTime? CreatedAt = null);

public sealed class GeminiService(HttpClient client, IConfiguration configuration)
{
    public const string ModelName = "gemini-3.5-flash-lite";
    public string Model => ModelName;

    public async Task<string> GenerateAsync(string instruction, IEnumerable<AiTurn> history, bool json, CancellationToken ct)
    {
        var apiKey = configuration["AI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI chưa được cấu hình. Hãy thêm Gemini API key ở backend.") { Data = { ["StatusCode"] = 503 } };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey.Trim());
        request.Content = JsonContent.Create(new
        {
            systemInstruction = new { parts = new[] { new { text = instruction } } },
            contents = history.Select(t => new { role = t.Role, parts = new[] { new { text = t.Text } } }),
            generationConfig = new { maxOutputTokens = 2048, responseMimeType = json ? "application/json" : "text/plain" }
        });
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new InvalidOperationException("Đã hết hạn mức Gemini tạm thời. Vui lòng thử lại sau.") { Data = { ["StatusCode"] = 429 } };
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Dịch vụ AI chưa sẵn sàng. Kiểm tra API key, model và hạn mức ở Google AI Studio.") { Data = { ["StatusCode"] = 503 } };
            using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!payload.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0 ||
                !candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
                throw new InvalidOperationException("AI không trả nội dung cho yêu cầu này. Hãy thử câu hỏi khác.") { Data = { ["StatusCode"] = 502 } };
            var text = string.Concat(parts.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("AI trả nội dung rỗng.") { Data = { ["StatusCode"] = 502 } };
            return text;
        }
        catch (HttpRequestException) { throw new InvalidOperationException("Không kết nối được Gemini. Vui lòng thử lại.") { Data = { ["StatusCode"] = 503 } }; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException("Gemini phản hồi quá lâu. Vui lòng thử lại.") { Data = { ["StatusCode"] = 504 } }; }
        catch (JsonException) { throw new InvalidOperationException("Phản hồi từ Gemini không hợp lệ.") { Data = { ["StatusCode"] = 502 } }; }
    }
}
