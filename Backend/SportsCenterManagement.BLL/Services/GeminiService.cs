using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Exceptions;

namespace SportsCenterManagement.BLL.Services;

public sealed record AiTurn(string Role, string Text);

public sealed class GeminiService(HttpClient client, IConfiguration configuration)
{
    public string Model => configuration["AI:Model"] ?? "gemini-3.5-flash-lite";

    public async Task<string> GenerateAsync(string instruction, IEnumerable<AiTurn> history, bool json, CancellationToken ct)
    {
        var apiKey = configuration["AI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new FlowException(503, "AI chưa được cấu hình. Hãy thêm Gemini API key ở backend.");
        if (!Regex.IsMatch(Model, @"^[a-zA-Z0-9._-]{1,100}$"))
            throw new FlowException(503, "Tên mô hình AI không hợp lệ.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
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
                throw new FlowException(429, "Đã hết hạn mức Gemini tạm thời. Vui lòng thử lại sau.");
            if (!response.IsSuccessStatusCode)
                throw new FlowException(503, "Dịch vụ AI chưa sẵn sàng. Kiểm tra API key, model và hạn mức ở Google AI Studio.");
            using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!payload.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0 ||
                !candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
                throw new FlowException(502, "AI không trả nội dung cho yêu cầu này. Hãy thử câu hỏi khác.");
            var text = string.Concat(parts.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
            if (string.IsNullOrWhiteSpace(text)) throw new FlowException(502, "AI trả nội dung rỗng.");
            return text;
        }
        catch (HttpRequestException) { throw new FlowException(503, "Không kết nối được Gemini. Vui lòng thử lại."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new FlowException(504, "Gemini phản hồi quá lâu. Vui lòng thử lại."); }
        catch (JsonException) { throw new FlowException(502, "Phản hồi từ Gemini không hợp lệ."); }
    }
}
