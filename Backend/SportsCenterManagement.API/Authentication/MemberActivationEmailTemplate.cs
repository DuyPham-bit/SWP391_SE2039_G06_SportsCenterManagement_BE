using System.Net;

namespace SportsCenterManagement.API.Authentication;

/// <summary>
/// "Minimalist luxury" activation email. Table layout + inline CSS only, so it renders in Gmail web/mobile and Outlook.
/// Fonts: Be Vietnam Pro where web fonts load (Apple Mail/iOS), otherwise system sans fonts with full Vietnamese
/// diacritic support. Serif fallbacks such as Georgia break stacked diacritics ("Thiê´t") and are not used.
/// Every interpolated value is HTML-encoded.
/// </summary>
public static class MemberActivationEmailTemplate
{
    public const string Subject = "Xác nhận email & thiết lập mật khẩu hội viên";

    private const string Font = "'Be Vietnam Pro','Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif";
    private const string Ink = "#2C2825";
    private const string Body = "#4A443E";
    private const string Muted = "#7A726A";
    private const string Accent = "#B38A65";
    private const string Cream = "#F7F5F0";
    private const string Line = "#E6E1D6";

    public static string BuildHtml(string fullName, string phone, string email, string verifyUrl, int validHours)
    {
        var name = WebUtility.HtmlEncode(fullName);
        var phoneText = WebUtility.HtmlEncode(phone);
        var emailText = WebUtility.HtmlEncode(email);
        var url = WebUtility.HtmlEncode(verifyUrl);

        // Stacked label/value rows read well at 320px and avoid cramped two-column wrapping on mobile.
        string Row(string label, string valueHtml, bool last = false) => $@"
                <tr>
                  <td style=""padding:0 0 {(last ? "0" : "14px")} 0;"">
                    <p style=""margin:0 0 3px 0;font-family:{Font};font-size:11px;line-height:16px;font-weight:600;letter-spacing:1px;text-transform:uppercase;color:{Muted};"">{label}</p>
                    <p style=""margin:0;font-family:{Font};font-size:15px;line-height:22px;font-weight:600;color:{Ink};word-break:break-word;"">{valueHtml}</p>
                  </td>
                </tr>";

        // Explicit anchors in the text colour stop Gmail from re-styling detected emails/phones in default blue.
        var emailHtml = $@"<a href=""mailto:{emailText}"" style=""color:{Ink};text-decoration:none;"">{emailText}</a>";
        var phoneHtml = $@"<a href=""tel:{phoneText}"" style=""color:{Ink};text-decoration:none;"">{phoneText}</a>";
        var passwordHtml = $@"<span style=""display:inline-block;padding:4px 10px;background-color:#FFFFFF;border:1px dashed {Accent};border-radius:6px;font-family:'SFMono-Regular',Menlo,Consolas,monospace;font-size:15px;letter-spacing:1px;color:{Ink};"">{phoneText}</span>";

        return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""x-apple-disable-message-reformatting"">
  <meta name=""format-detection"" content=""telephone=no, address=no, email=no"">
  <title>{Subject}</title>
  <link href=""https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;600;700&amp;display=swap"" rel=""stylesheet"">
</head>
<body style=""margin:0;padding:0;background-color:{Cream};-webkit-font-smoothing:antialiased;"">
  <div style=""display:none;max-height:0;overflow:hidden;opacity:0;"">Kích hoạt tài khoản hội viên và đặt mật khẩu mới trong {validHours} giờ.</div>
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:{Cream};"">
    <tr>
      <td align=""center"" style=""padding:40px 16px 24px 16px;"">
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:560px;background-color:#FFFFFF;border:1px solid {Line};border-radius:12px;overflow:hidden;"">
          <tr><td style=""height:6px;line-height:6px;font-size:0;background-color:{Accent};"">&nbsp;</td></tr>

          <tr>
            <td style=""padding:40px 40px 0 40px;"">
              <p style=""margin:0 0 14px 0;font-family:{Font};font-size:12px;line-height:16px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:{Accent};"">MEMBERSHIP PORTAL</p>
              <h1 style=""margin:0;font-family:{Font};font-size:26px;line-height:34px;font-weight:700;letter-spacing:-0.2px;color:{Ink};"">Xác Nhận &amp; Thiết Lập Mật Khẩu</h1>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:20px 0 24px 0;""><tr><td style=""width:40px;height:2px;line-height:2px;font-size:0;background-color:{Accent};"">&nbsp;</td></tr></table>
              <p style=""margin:0 0 12px 0;font-family:{Font};font-size:15px;line-height:24px;color:{Ink};"">Xin chào <strong>{name}</strong>,</p>
              <p style=""margin:0 0 28px 0;font-family:{Font};font-size:15px;line-height:24px;color:{Body};"">Tài khoản hội viên của bạn vừa được lễ tân khởi tạo tại quầy. Vui lòng xác nhận địa chỉ email và đặt mật khẩu riêng để bảo vệ tài khoản.</p>
            </td>
          </tr>

          <tr>
            <td style=""padding:0 40px;"">
              <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:{Cream};border-left:4px solid {Accent};border-radius:0 8px 8px 0;"">
                <tr>
                  <td style=""padding:22px 24px;"">
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">{Row("Họ và tên", name)}{Row("Số điện thoại", phoneHtml)}{Row("Email đăng nhập", emailHtml)}{Row("Mật khẩu mặc định ban đầu", passwordHtml, last: true)}
                    </table>
                    <p style=""margin:16px 0 0 0;padding-top:14px;border-top:1px solid {Line};font-family:{Font};font-size:13px;line-height:20px;color:{Muted};"">Bạn sẽ được yêu cầu đổi mật khẩu mới khi bấm xác nhận bên dưới.</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td align=""center"" style=""padding:32px 40px 12px 40px;"">
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                <tr>
                  <td align=""center"" bgcolor=""{Ink}"" style=""border-radius:8px;background-color:{Ink};"">
                    <a href=""{url}"" target=""_blank"" style=""display:inline-block;padding:15px 36px;font-family:{Font};font-size:14px;line-height:20px;font-weight:700;letter-spacing:1px;color:#FFFFFF;text-decoration:none;border-radius:8px;"">XÁC NHẬN &amp; ĐỔI MẬT KHẨU NGAY</a>
                  </td>
                </tr>
              </table>
              <p style=""margin:14px 0 0 0;font-family:{Font};font-size:12px;line-height:18px;color:{Muted};"">Liên kết có hiệu lực trong <strong style=""color:{Ink};"">{validHours} giờ</strong> và chỉ dùng được một lần.</p>
            </td>
          </tr>

          <tr>
            <td style=""padding:28px 40px 36px 40px;"">
              <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-top:1px solid {Line};"">
                <tr>
                  <td style=""padding-top:24px;"">
                    <p style=""margin:0 0 6px 0;font-family:{Font};font-size:12px;line-height:18px;color:{Muted};"">Nút không hoạt động? Sao chép đường dẫn sau vào trình duyệt:</p>
                    <p style=""margin:0 0 16px 0;font-family:{Font};font-size:12px;line-height:18px;word-break:break-all;""><a href=""{url}"" target=""_blank"" style=""color:{Accent};text-decoration:underline;"">{url}</a></p>
                    <p style=""margin:0;font-family:{Font};font-size:12px;line-height:18px;color:{Muted};"">Nếu bạn không đăng ký hội viên tại trung tâm, vui lòng bỏ qua email này hoặc liên hệ quầy lễ tân.</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
        </table>

        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:560px;"">
          <tr>
            <td align=""center"" style=""padding:20px 16px 0 16px;font-family:{Font};font-size:11px;line-height:18px;color:{Muted};"">
              Email tự động từ hệ thống SCMS · Vui lòng không trả lời email này.
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    public static string BuildText(string fullName, string phone, string email, string verifyUrl, int validHours) =>
        $"Xin chào {fullName},\n\n" +
        "Tài khoản hội viên của bạn vừa được khởi tạo tại quầy lễ tân.\n\n" +
        $"Họ và tên: {fullName}\nSố điện thoại: {phone}\nEmail đăng nhập: {email}\nMật khẩu mặc định ban đầu: {phone}\n\n" +
        $"Xác nhận email và đặt mật khẩu mới tại: {verifyUrl}\n\n" +
        $"Liên kết có hiệu lực trong {validHours} giờ và chỉ dùng được một lần.";
}
