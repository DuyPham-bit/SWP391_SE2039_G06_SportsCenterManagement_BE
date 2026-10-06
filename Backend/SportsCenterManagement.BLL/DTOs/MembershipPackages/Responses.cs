namespace SportsCenterManagement.BLL.DTOs.MembershipPackages;

public static class Responses
{
    public sealed record MembershipPackageResponse(
        long Id,
        long CenterId,
        string Name,
        string? Description,
        int DurationDays,
        decimal Price,
        int? MaxClasses,
        string? AccessType,
        string Status);
}
