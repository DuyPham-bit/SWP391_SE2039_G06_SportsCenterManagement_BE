using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed record RecordCashPaymentRequest([property: Range(typeof(decimal), "0.01", "9999999999")] decimal Amount);
