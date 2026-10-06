using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO tiếp nhận yêu cầu hủy giao dịch nhầm tại quầy.
/// </summary>
public class VoidPaymentRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hủy giao dịch.")]
    [MinLength(5, ErrorMessage = "Lý do hủy giao dịch phải từ 5 ký tự trở lên.")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
