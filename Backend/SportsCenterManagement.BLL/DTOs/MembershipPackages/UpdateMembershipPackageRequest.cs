using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.MembershipPackages;

public sealed record UpdateMembershipPackageRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: StringLength(500)] string? Description,
    [property: Range(1, 3650)] int DurationDays,
    [property: Range(typeof(decimal), "0.01", "9999999999")] decimal Price,
    [property: Range(1, 10000)] int? MaxClasses,
    [property: StringLength(50)] string? AccessType,
    [property: Required, RegularExpression("^(Draft|Active|Inactive)$")] string Status);
