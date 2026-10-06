using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.MembershipPackages;

public sealed record SaveMembershipPackageRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: StringLength(500)] string? Description,
    [property: Range(1, 3650)] int DurationDays,
    [property: Range(typeof(decimal), "0.01", "9999999999")] decimal Price,
    [property: Range(1, 1000)] int? MaxClasses,
    [property: StringLength(50)] string? AccessType,
    [property: Range(1, 100)] int AllowedSports,
    [property: StringLength(50)] string? Badge,
    IReadOnlyList<string>? Features);
