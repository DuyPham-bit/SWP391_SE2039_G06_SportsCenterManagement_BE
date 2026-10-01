using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Members;

public sealed record CreateSubscriptionRequest([property: Range(1, long.MaxValue)] long PackageId);
