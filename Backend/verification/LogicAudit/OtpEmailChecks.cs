using System.Net;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.API.Authentication;

/// <summary>Registration OTP email mirrors the Vercel security-code email, including the request location.</summary>
static class OtpEmailChecks
{
    public static async Task Run(HttpClient http, FakeMailGateway mail, IConfiguration configuration,
        Func<string, Func<Task<bool>>, Task> check)
    {
        async Task<(string Title, string Message, string Html)?> Issue(string email, string? city, string? country)
        {
            http.DefaultRequestHeaders.Authorization = null;
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/request-email-verification")
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { email })
            };
            if (city is not null) request.Headers.TryAddWithoutValidation("CF-IPCity", city);
            if (country is not null) request.Headers.Add("CF-IPCountry", country);
            var before = mail.Messages.Count;
            var response = await http.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.OK || mail.Messages.Count != before + 1) return null;
            var sent = mail.Messages[^1];
            return (sent.GetProperty("title").GetString()!, sent.GetProperty("message").GetString()!, sent.GetProperty("html").GetString()!);
        }

        await check("registration OTP email keeps the exact Vercel subject, wording and expiry line", async () =>
        {
            var mailOut = await Issue("vercel-format@example.test", null, null);
            if (mailOut is not { } m) return false;
            var otp = mail.Otp;
            return m.Title == $"Your SCMS security code: {otp}"
                && m.Message == $"We received a request from {RequestLocation.Default} to confirm the following action on your SCMS account: confirm account registration. Use this code:\n\n{otp}\n\nThis code expires in 5 minutes. If you did not request it, you can safely ignore this email."
                && m.Html.Contains($"<strong>{RequestLocation.Default}</strong>") && m.Html.Contains($"<strong>{otp}</strong>");
        });

        await check("registration OTP email ignores location headers unless the proxy is trusted", async () =>
        {
            var m = await Issue("untrusted-location@example.test", "Hanoi", "VN");
            return m is { } value && value.Message.Contains($"from {RequestLocation.Default} to confirm") && !value.Message.Contains("Hanoi");
        });

        configuration[RequestLocation.TrustSetting] = "true";
        try
        {
            await check("registration OTP email shows the Cloudflare request city and country when trusted", async () =>
            {
                var m = await Issue("trusted-location@example.test", "Hanoi", "VN");
                return m is { } value && value.Message.Contains("from Hanoi, Vietnam to confirm")
                    && value.Html.Contains("<strong>Hanoi, Vietnam</strong>");
            });

            await check("registration OTP email never places injected header text in the email", async () =>
            {
                var injected = await Issue("injected-location@example.test", "Call 0900 http://evil.test <b>", "VN");
                var unknown = await Issue("unknown-location@example.test", "Hanoi", "XX");
                return injected is { } a && a.Message.Contains("from Vietnam to confirm") && !a.Html.Contains("evil")
                    && unknown is { } b && b.Message.Contains($"from {RequestLocation.Default} to confirm");
            });
        }
        finally
        {
            configuration[RequestLocation.TrustSetting] = null;
        }
    }
}
