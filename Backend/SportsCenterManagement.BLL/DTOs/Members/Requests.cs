using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Members
{
    public static class Requests
    {
        public sealed record CreateSubscriptionRequest([property: Range(1, long.MaxValue)] long PackageId);

        public sealed record UpdateMemberProfileRequest(
            [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [property: EmailAddress, StringLength(150)] string? Email,
            [property: StringLength(20)] string? Phone,
            DateOnly? DateOfBirth,
            [property: StringLength(20)] string? Gender,
            [property: StringLength(255)] string? Address);

        public sealed record SetMemberStatusRequest(
            [property: Required, RegularExpression("^(Active|Disabled)$")] string Status);
    }
}
