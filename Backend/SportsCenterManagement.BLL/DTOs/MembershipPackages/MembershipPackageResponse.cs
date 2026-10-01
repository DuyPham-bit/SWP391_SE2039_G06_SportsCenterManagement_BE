namespace SportsCenterManagement.BLL.DTOs.MembershipPackages;

public sealed record MembershipPackageResponse(
    long Id,
    long CenterId,
    string Name,
    string? Description,
    int DurationDays,
    decimal Price,
    int? MaxClasses,
    string? AccessType);
