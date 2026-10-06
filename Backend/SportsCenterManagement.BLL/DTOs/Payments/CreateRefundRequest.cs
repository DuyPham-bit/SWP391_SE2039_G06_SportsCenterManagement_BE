using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed class CreateRefundRequest
{
    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal Amount { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ExternalRefundReference { get; set; }
}
