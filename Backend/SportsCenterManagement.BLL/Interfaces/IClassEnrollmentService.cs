using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IClassEnrollmentService
{
    Task<IReadOnlyList<MemberEnrollmentDto>> GetMyEnrollmentsAsync(long userId, CancellationToken ct = default);
    Task<ClassEnrollmentResultDto> EnrollAsync(long userId, long classId, long subscriptionId, CancellationToken ct = default);
    Task<CancelledClassEnrollmentResultDto> CancelEnrollmentAsync(long userId, long classId, long enrollmentId, string? reason, CancellationToken ct = default);
}

