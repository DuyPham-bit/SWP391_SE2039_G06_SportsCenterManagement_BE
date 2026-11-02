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

    public async Task<bool> SendAsync(User recipient, PasswordResetToken reset, string otp, CancellationToken ct)
    {
        if (!IsConfigured) return false;
        // OTPs never enter the notification inbox, database payloads, or provider response logs.
        var delivery = new CommunicationDelivery
        {
            RecipientUserId = recipient.Id, Channel = "Email", Status = "Sending", CreatedAt = DateTime.UtcNow
        };
        db.CommunicationDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
        var title = "[SCMS] Mã xác thực đặt lại mật khẩu";
        var message = $"Mã xác thực đặt lại mật khẩu của bạn tại SCMS: {otp}. Mã hết hạn sau 10 phút. Tuyệt đối không chia sẻ mã này với người khác.";
        var html = BuildOtpEmailHtml(
            "Yêu Cầu Đặt Lại Mật Khẩu",
            recipient.Email,
            "Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn tại Hệ thống Quản lý Trung tâm Thể thao SCMS. Vui lòng sử dụng mã xác thực bên dưới để tiếp tục:",
            otp,
            10);
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

    public async Task<bool> SendVerificationAsync(EmailVerificationToken verification, string otp, CancellationToken ct)
    {
        var title = "[SCMS] Mã xác thực đăng ký tài khoản";
        var message = $"Mã xác thực đăng ký tài khoản của bạn tại SCMS: {otp}. Mã hết hạn sau 10 phút. Tuyệt đối không chia sẻ mã này với người khác.";
        var html = BuildOtpEmailHtml(
            "Xác Thực Email Đăng Ký Tài Khoản",
            verification.Email,
            "Cảm ơn bạn đã đăng ký tài khoản tại Hệ thống Quản lý Trung tâm Thể thao SCMS. Vui lòng sử dụng mã xác thực bên dưới để hoàn tất việc kích hoạt tài khoản của bạn:",
            otp,
            10);
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
                if (string.IsNullOrWhiteSpace(displayName)) displayName = "SCMS - Trung Tâm Thể Thao";
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

    private static string BuildOtpEmailHtml(
        string headerTitle,
        string recipientEmail,
        string introText,
        string otp,
        int expiryMinutes = 10)
    {
        return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{headerTitle}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; -webkit-font-smoothing: antialiased; color: #1e293b;"">
  <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #f1f5f9; padding: 32px 16px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 560px; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.08), 0 8px 10px -6px rgba(0, 0, 0, 0.04); border: 1px solid #e2e8f0;"">
          <!-- Top Accent Bar -->
          <tr>
            <td style=""height: 6px; background: linear-gradient(90deg, #1A3B8B 0%, #2563EB 50%, #F26522 100%);""></td>
          </tr>
          <!-- Header -->
          <tr>
            <td style=""padding: 32px 36px 20px 36px; text-align: center; border-bottom: 1px solid #f1f5f9;"">
              <div style=""display: inline-block; background-color: #ebf2fc; border-radius: 12px; padding: 8px 18px; margin-bottom: 12px;"">
                <span style=""font-size: 16px; font-weight: 800; color: #1A3B8B; letter-spacing: 1.5px;"">⚽ SCMS SPORTS</span>
              </div>
              <h1 style=""margin: 0; font-size: 21px; font-weight: 700; color: #0f172a; line-height: 1.3;"">{headerTitle}</h1>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style=""padding: 28px 36px 32px 36px;"">
              <p style=""margin: 0 0 14px 0; font-size: 15px; line-height: 1.6; color: #334155;"">
                Xin chào <strong>{recipientEmail}</strong>,
              </p>
              <p style=""margin: 0 0 24px 0; font-size: 15px; line-height: 1.6; color: #475569;"">
                {introText}
              </p>

              <!-- OTP Code Highlight Box -->
              <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #f8fafc; border: 2px dashed #1A3B8B; border-radius: 14px; margin: 20px 0;"">
                <tr>
                  <td align=""center"" style=""padding: 24px 20px;"">
                    <div style=""font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 2.5px; color: #64748b; margin-bottom: 10px;"">
                      MÃ XÁC THỰC CỦA BẠN (OTP)
                    </div>
                    <div style=""font-family: 'Consolas', 'Courier New', Courier, monospace; font-size: 40px; font-weight: 800; letter-spacing: 12px; color: #1A3B8B; line-height: 1; padding: 4px 0;"">
                      {otp}
                    </div>
                    <div style=""margin-top: 14px;"">
                      <span style=""display: inline-block; background-color: #fef3c7; color: #92400e; font-size: 12px; font-weight: 600; padding: 4px 14px; border-radius: 20px; border: 1px solid #fde68a;"">
                        ⏱ Hiệu lực trong vòng {expiryMinutes} phút
                      </span>
                    </div>
                  </td>
                </tr>
              </table>

              <!-- Security Advisory Box -->
              <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #fffbeb; border-left: 4px solid #f59e0b; border-radius: 6px; margin-top: 20px;"">
                <tr>
                  <td style=""padding: 14px 16px; font-size: 13px; line-height: 1.5; color: #78350f;"">
                    <strong>Lưu ý bảo mật:</strong><br/>
                    • Tuyệt đối <strong>không chia sẻ</strong> mã OTP này với bất kỳ ai, kể cả nhân viên SCMS.<br/>
                    • Nếu bạn không yêu cầu mã này, vui lòng bỏ qua email để đảm bảo an toàn cho tài khoản.
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style=""background-color: #f8fafc; padding: 22px 36px; border-top: 1px solid #e2e8f0; text-align: center;"">
              <p style=""margin: 0 0 6px 0; font-size: 12px; font-weight: 600; color: #475569;"">
                Hệ Thống Quản Lý Trung Tâm Thể Thao SCMS
              </p>
              <p style=""margin: 0; font-size: 11px; line-height: 1.6; color: #94a3b8;"">
                Hotline hỗ trợ: <strong style=""color: #64748b;"">0859 859 367</strong> | Email: <strong style=""color: #64748b;"">duypvd4@gmail.com</strong><br/>
                Email này được gửi tự động, vui lòng không phản hồi trực tiếp.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }
}
