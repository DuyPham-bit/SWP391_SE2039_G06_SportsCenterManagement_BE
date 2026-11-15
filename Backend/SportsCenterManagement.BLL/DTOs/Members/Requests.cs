using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SportsCenterManagement.BLL.DTOs.Members
{
    public static class Requests
    {
        public sealed record CreateSubscriptionRequest(
            [param: Range(1, long.MaxValue)] long PackageId,
            [param: MaxLength(20)] List<long>? SportIds = null);

        public sealed record UpdateMemberProfileRequest(
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [param: EmailAddress, StringLength(150)] string? Email,
            [param: StringLength(20)] string? Phone,
            DateOnly? DateOfBirth,
            [param: StringLength(20)] string? Gender,
            [param: StringLength(255)] string? Address,
            [param: StringLength(50)] string? FitnessLevel = null);

        [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
        public sealed record UpdateMemberAtCenterRequest(
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [param: StringLength(20)] string? Phone,
            DateOnly? DateOfBirth,
            [param: StringLength(20)] string? Gender,
            [param: StringLength(255)] string? Address);

        /// <summary>Counter-safe fields only: no login phone/email, address or status.</summary>
        public sealed record UpdateMemberBasicInfoRequest(
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            DateOnly? DateOfBirth,
            [param: StringLength(20)] string? Gender,
            [param: StringLength(150)] string? EmergencyContactName,
            [param: StringLength(20)] string? EmergencyContactPhone);

        public sealed record SetMemberStatusRequest(
            [param: Required, RegularExpression("^(Active|Disabled)$")] string Status);
    }
}
