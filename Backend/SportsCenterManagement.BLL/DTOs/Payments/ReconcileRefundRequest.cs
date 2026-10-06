using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed class ReconcileRefundRequest
{
    [Required]
    [RegularExpression("^(Succeeded|Failed)$")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ProviderRefundId { get; set; }

    [Required]
    [MinLength(10)]
    [MaxLength(500)]
    public string EvidenceNote { get; set; } = string.Empty;
}
