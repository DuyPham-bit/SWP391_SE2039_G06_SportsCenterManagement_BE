using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.MembershipPackages
{
    public sealed record SaveMembershipPackageRequest(
        [param: Required, StringLength(150, MinimumLength = 1)] string Name,
        [param: StringLength(500)] string? Description,
        [param: Range(1, 3650)] int DurationDays,
        [param: Range(typeof(decimal), "0.01", "9999999999")] decimal Price,
        [param: Range(1, 1000)] int? MaxClasses,
        [param: StringLength(50)] string? AccessType,
        [param: Range(1, 100)] int AllowedSports,
        [param: StringLength(50)] string? Badge,
        IReadOnlyList<string>? Features);

    public static class Requests
    {
        public sealed record CreateMembershipPackageRequest(
            [param: Required, StringLength(150, MinimumLength = 1)] string Name,
            [param: StringLength(500)] string? Description,
            [param: Range(1, 3650)] int DurationDays,
            [param: Range(typeof(decimal), "0.01", "9999999999")] decimal Price,
            [param: Range(1, 10000)] int? MaxClasses,
            [param: StringLength(50)] string? AccessType,
            [param: Required, RegularExpression("^(Draft|Active)$")] string Status);

        public sealed record UpdateMembershipPackageRequest(
            [param: Required, StringLength(150, MinimumLength = 1)] string Name,
            [param: StringLength(500)] string? Description,
            [param: Range(1, 3650)] int DurationDays,
            [param: Range(typeof(decimal), "0.01", "9999999999")] decimal Price,
            [param: Range(1, 10000)] int? MaxClasses,
            [param: StringLength(50)] string? AccessType,
            [param: Required, RegularExpression("^(Draft|Active|Inactive)$")] string Status);

        public sealed record SetMembershipPackageStatusRequest(string Status);
    }
}
