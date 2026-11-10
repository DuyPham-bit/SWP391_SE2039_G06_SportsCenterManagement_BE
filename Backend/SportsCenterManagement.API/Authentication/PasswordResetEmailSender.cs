using System.Net.Http.Headers;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

using Microsoft.Extensions.Logging;

namespace SportsCenterManagement.API.Authentication;

public sealed class PasswordResetEmailSender(
    SportsCenterDbContext db, IHttpClientFactory clients, IConfiguration configuration,
    ILogger<PasswordResetEmailSender>? logger = null)
{
    private Uri? Endpoint => Uri.TryCreate(configuration["ExternalCommunications:Email:WebhookUrl"], UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ? uri : null;

    private string? SmtpHost => string.IsNullOrWhiteSpace(configuration["ExternalCommunications:Email:Smtp:Host"])
        ? configuration["MailSettings:Host"]
        : configuration["ExternalCommunications:Email:Smtp:Host"];
    private string? SmtpFrom => string.IsNullOrWhiteSpace(configuration["ExternalCommunications:Email:Smtp:From"])
        ? configuration["MailSettings:Mail"]
        : configuration["ExternalCommunications:Email:Smtp:From"];
    private bool SmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && MailAddress.TryCreate(SmtpFrom, out _);
    public bool IsConfigured => Endpoint is not null || SmtpConfigured;

    public async Task<bool> SendAsync(
        User recipient,
        PasswordResetToken reset,
        string otp,
        CancellationToken ct,
        string actionEn = "reset password",
        string location = RequestLocation.Default)
    {
        if (!IsConfigured) return false;
        // OTPs never enter the notification inbox, database payloads, or provider response logs.
        var delivery = new CommunicationDelivery
        {
            RecipientUserId = recipient.Id, Channel = "Email", Status = "Sending", CreatedAt = DateTime.UtcNow
        };
        db.CommunicationDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
        var (title, message, html) = BuildOtpEmail(actionEn, otp, location);
        var result = await SendCodeAsync(recipient.Email, title, message, html, $"password-reset-{reset.Id}", new
        {
            deliveryId = delivery.Id, channel = "Email", recipientUserId = recipient.Id, to = recipient.Email,
            title, message, html,
            notificationType = "PasswordReset", createdAt = reset.CreatedAt, expiresAt = reset.ExpiresAt
        }, ct);
        delivery.Status = result.Success ? "Sent" : "Failed";
        delivery.SentAt = result.Success ? DateTime.UtcNow : null;
        delivery.ProviderMessage = result.Status;
        if (!result.Success)
        {
            await db.PasswordResetTokens.Where(t => t.Id == reset.Id && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), CancellationToken.None);
        }
        await db.SaveChangesAsync(CancellationToken.None);
        return result.Success;
    }

    public async Task<bool> SendVerificationAsync(
        EmailVerificationToken verification, string otp, CancellationToken ct, string location = RequestLocation.Default)
    {
        var (title, message, html) = BuildOtpEmail("confirm account registration", otp, location);
        var result = await SendCodeAsync(verification.Email, title, message, html, $"email-verification-{verification.Id}", new
        {
            channel = "Email", to = verification.Email, title, message, html,
            notificationType = "EmailVerification", createdAt = verification.CreatedAt, expiresAt = verification.ExpiresAt
        }, ct);
        if (!result.Success)
            await db.EmailVerificationTokens.Where(t => t.Id == verification.Id && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), CancellationToken.None);
        return result.Success;
    }

    /// <summary>Sends the counter-created member's activation link. The link is never persisted or logged.</summary>
    public async Task<bool> SendMemberActivationAsync(
        long recipientUserId, string fullName, string phone, string email, string verifyUrl,
        DateTime expiresAt, string idempotencyKey, CancellationToken ct)
    {
        if (!IsConfigured) return false;
        var delivery = new CommunicationDelivery
        {
            RecipientUserId = recipientUserId, Channel = "Email", Status = "Sending", CreatedAt = DateTime.UtcNow
        };
        db.CommunicationDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
        var hours = (int)Math.Round(SportsCenterManagement.BLL.Services.MemberActivationService.LinkLifetime.TotalHours);
        var title = MemberActivationEmailTemplate.Subject;
        var message = MemberActivationEmailTemplate.BuildText(fullName, phone, email, verifyUrl, hours);
        var html = MemberActivationEmailTemplate.BuildHtml(fullName, phone, email, verifyUrl, hours);
        var result = await SendCodeAsync(email, title, message, html, idempotencyKey, new
        {
            deliveryId = delivery.Id, channel = "Email", recipientUserId, to = email,
            title, message, html, notificationType = "MemberActivation", expiresAt
        }, ct);
        delivery.Status = result.Success ? "Sent" : "Failed";
        delivery.SentAt = result.Success ? DateTime.UtcNow : null;
        delivery.ProviderMessage = result.Status;
        await db.SaveChangesAsync(CancellationToken.None);
        return result.Success;
    }

    private async Task<(bool Success, string Status)> SendCodeAsync(
        string email, string title, string message, string htmlMessage, string key, object payload, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            if (Endpoint is not { } endpoint)
            {
                var smtpHost = SmtpHost;
                var smtpFrom = SmtpFrom;
                if (string.IsNullOrWhiteSpace(smtpHost) || !MailAddress.TryCreate(smtpFrom, out _))
                    return (false, "Email sender unavailable.");
                var port = configuration.GetValue("ExternalCommunications:Email:Smtp:Port", 0);
                if (port <= 0) port = configuration.GetValue("MailSettings:Port", 587);
                var enableSsl = configuration.GetValue("ExternalCommunications:Email:Smtp:EnableSsl", true);
                if (!enableSsl && !string.Equals(smtpHost, "localhost", StringComparison.OrdinalIgnoreCase)
                    && !(IPAddress.TryParse(smtpHost, out var address) && IPAddress.IsLoopback(address)))
                    return (false, "SMTP requires TLS.");
                var username = configuration["ExternalCommunications:Email:Smtp:Username"];
                if (string.IsNullOrWhiteSpace(username)) username = configuration["MailSettings:Mail"];
                var password = configuration["ExternalCommunications:Email:Smtp:Password"];
                if (string.IsNullOrWhiteSpace(password)) password = configuration["MailSettings:Password"];

                using var client = new MailKit.Net.Smtp.SmtpClient();
                var secureOption = enableSsl
                    ? MailKit.Security.SecureSocketOptions.StartTls
                    : MailKit.Security.SecureSocketOptions.Auto;
                await client.ConnectAsync(smtpHost, port, secureOption, timeout.Token);
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    await client.AuthenticateAsync(username, password, timeout.Token);
                }
                var mime = new MimeKit.MimeMessage();
                var displayName = configuration["MailSettings:DisplayName"];
                if (string.IsNullOrWhiteSpace(displayName)) displayName = configuration["ExternalCommunications:Email:Smtp:DisplayName"];
                if (string.IsNullOrWhiteSpace(displayName) || displayName.Contains("Trung Tâm Thể Thao") || displayName.Contains("Olympic") || displayName.Contains("Hệ Thống")) displayName = "SCMS";
                mime.From.Add(new MimeKit.MailboxAddress(displayName, smtpFrom));
                mime.To.Add(MimeKit.MailboxAddress.Parse(email));
                mime.Subject = title;
                var builder = new MimeKit.BodyBuilder
                {
                    TextBody = message,
                    HtmlBody = htmlMessage
                };
                mime.Body = builder.ToMessageBody();
                await client.SendAsync(mime, timeout.Token);
                await client.DisconnectAsync(true, timeout.Token);
                return (true, "SMTP accepted");
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            var bearer = configuration["ExternalCommunications:Email:BearerToken"];
            if (string.IsNullOrWhiteSpace(bearer)) bearer = configuration["ExternalCommunications:BearerToken"];
            if (!string.IsNullOrWhiteSpace(bearer)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.Add("Idempotency-Key", key);
            request.Content = JsonContent.Create(payload);
            using var response = await clients.CreateClient(nameof(PasswordResetEmailSender)).SendAsync(request, timeout.Token);
            return (response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception)
        {
            logger?.LogWarning("Email delivery failed for {Email}: {Error}", email, exception.Message);
            // Provider errors can contain credentials, email content or OTPs. Keep only a safe status.
            return (false, "Email gateway unavailable.");
        }
    }

    private const int OtpExpiryMinutes = 5;

    /// <summary>Vercel-style security code email: same subject, wording and layout for text and HTML parts.</summary>
    private static (string Title, string Message, string Html) BuildOtpEmail(string actionEn, string otp, string location)
    {
        var title = $"Your SCMS security code: {otp}";
        var message = $"We received a request from {location} to confirm the following action on your SCMS account: {actionEn}. Use this code:\n\n{otp}\n\nThis code expires in {OtpExpiryMinutes} minutes. If you did not request it, you can safely ignore this email.";
        return (title, message, BuildOtpEmailHtml(actionEn, otp, System.Net.WebUtility.HtmlEncode(location), OtpExpiryMinutes));
    }

    private static string BuildOtpEmailHtml(
        string actionEn,
        string otp,
        string encodedLocation,
        int expiryMinutes)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Your SCMS security code: {otp}</title>
</head>
<body style=""margin: 0; padding: 24px 0; background-color: #ffffff; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased; color: #000000; max-width: 560px;"">
  <p style=""margin: 0 0 24px 0; font-size: 14px; line-height: 24px; color: #000000;"">
    We received a request from <strong>{encodedLocation}</strong> to confirm the following action on your SCMS account: <strong>{actionEn}</strong>. Use this code:
  </p>
  <p style=""margin: 0 0 24px 0; font-size: 24px; font-weight: 700; line-height: 24px; color: #000000; letter-spacing: 0.5px;"">
    <strong>{otp}</strong>
  </p>
  <p style=""margin: 0; font-size: 14px; line-height: 24px; color: #000000;"">
    This code expires in {expiryMinutes} minutes. If you did not request it, you can safely ignore this email.
  </p>
</body>
</html>";
    }
}