using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.API.Models
{
    public class MailContent
    {
        [Required, EmailAddress, StringLength(254)]
        public string To { get; set; } = string.Empty;          // Địa chỉ email người nhận
        [Required, StringLength(200)]
        public string Subject { get; set; } = string.Empty;     // Tiêu đề email
        [Required, StringLength(20000)]
        public string Body { get; set; } = string.Empty;        // Nội dung email (có thể chứa HTML)
    }
}
