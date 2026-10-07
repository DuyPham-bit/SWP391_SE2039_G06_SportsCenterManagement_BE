using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Members
{
    public static class Requests
    {
        public sealed record CreateSubscriptionRequest([param: Range(1, long.MaxValue)] long PackageId);

        public sealed record UpdateMemberProfileRequest(
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [param: EmailAddress, StringLength(150)] string? Email,
            [param: StringLength(20)] string? Phone,
            DateOnly? DateOfBirth,
            [param: StringLength(20)] string? Gender,
            [param: StringLength(255)] string? Address);

        public sealed record SetMemberStatusRequest(
            [param: Required, RegularExpression("^(Active|Disabled)$")] string Status);
    }
}
