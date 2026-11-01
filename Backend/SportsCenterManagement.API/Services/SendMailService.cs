using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SportsCenterManagement.API.Models;

namespace SportsCenterManagement.API.Services
{
    public class SendMailService : ISendMailService
    {
        private readonly MailSettings _mailSettings;

        // Inject IOptions<MailSettings> để lấy cấu hình từ appsettings.json
        public SendMailService(IOptions<MailSettings> mailSettings)
        {
            _mailSettings = mailSettings.Value;
        }

        public async Task<string> SendMailAsync(MailContent mailContent)
        {
            var email = new MimeMessage();

            // 1. Cấu hình người gửi (From)
            email.Sender = new MailboxAddress(_mailSettings.DisplayName, _mailSettings.Mail);
            email.From.Add(email.Sender);

            // 2. Cấu hình người nhận (To)
            email.To.Add(MailboxAddress.Parse(mailContent.To));

            // 3. Cấu hình Tiêu đề (Subject)
            email.Subject = mailContent.Subject;

            // 4. Cấu hình Nội dung (Body - hỗ trợ HTML)
            var builder = new BodyBuilder();
            builder.HtmlBody = mailContent.Body;
            email.Body = builder.ToMessageBody();

            // 5. Khởi tạo SmtpClient của MailKit để kết nối & xác thực
            using var smtp = new SmtpClient();

            try
            {
                // Mở kết nối tới máy chủ SMTP
                await smtp.ConnectAsync(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls);

                // XÁC THỰC TÀI KHOẢN (Verify / Authenticate Mail)
                await smtp.AuthenticateAsync(_mailSettings.Mail, _mailSettings.Password);

                // Gửi thư
                await smtp.SendAsync(email);

                // Ngắt kết nối
                await smtp.DisconnectAsync(true);

                return "Gửi email thành công!";
            }
            catch (Exception ex)
            {
                // Bắt lỗi nếu thông tin xác thực sai hoặc kết nối thất bại
                return $"Lỗi gửi mail: {ex.Message}";
            }
        }
    }
}
