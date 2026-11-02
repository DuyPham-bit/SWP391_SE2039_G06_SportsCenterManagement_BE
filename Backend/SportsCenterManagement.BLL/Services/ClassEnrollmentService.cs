using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class ClassEnrollmentService(
    IClassRepository classRepository,
    ICoreFlowService coreFlowService) : IClassEnrollmentService
{
    public async Task<IReadOnlyList<MemberEnrollmentDto>> GetMyEnrollmentsAsync(long userId, CancellationToken ct = default)
    {
        var memberId = await classRepository.GetMemberIdByUserIdAsync(userId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy hồ sơ Member.");

        var rows = await classRepository.GetMemberEnrollmentsAsync(memberId, ct);

        return rows.Select(x => new MemberEnrollmentDto(
            x.Id,
            x.ClassId,
            x.Name,
            x.SubscriptionId,
            x.RegisteredAt,
            x.CancelledAt,
            x.Status,
            x.CancellationReason)).ToList();
    }

    public async Task<ClassEnrollmentResultDto> EnrollAsync(long userId, long classId, long subscriptionId, CancellationToken ct = default)
    {
        var memberId = await classRepository.GetMemberIdByUserIdAsync(userId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy hồ sơ Member.");

        try
        {
            var enrollment = await coreFlowService.EnrollMemberAsync(classId, memberId, subscriptionId, userId, ct);
            return new ClassEnrollmentResultDto(
                enrollment.Id,
                enrollment.ClassId,
                enrollment.MemberId,
                enrollment.SubscriptionId,
                enrollment.RegisteredAt,
                enrollment.Status);
        }
        catch (InvalidOperationException ex)
        {
            var conflict = ex.Message.Contains("full", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already enrolled", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("allowance", StringComparison.OrdinalIgnoreCase);

            if (conflict)
            {
                throw BusinessException.Conflict(ex.Message);
            }
            throw BusinessException.BadRequest(ex.Message);
        }
    }

    public async Task<CancelledClassEnrollmentResultDto> CancelEnrollmentAsync(long userId, long classId, long enrollmentId, string? reason, CancellationToken ct = default)
    {
        var memberId = await classRepository.GetMemberIdByUserIdAsync(userId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy hồ sơ Member.");

        var exists = await classRepository.ExistsEnrollmentAsync(enrollmentId, classId, memberId, ct);
        if (!exists)
        {
            throw BusinessException.NotFound("Không tìm thấy ghi danh.");
        }

        try
        {
            var enrollment = await coreFlowService.CancelClassEnrollmentAsync(classId, memberId, reason, ct);
            return new CancelledClassEnrollmentResultDto(
                enrollment.Id,
                enrollment.ClassId,
                enrollment.Status,
                enrollment.CancelledAt,
                enrollment.CancellationReason);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("2 tiếng", StringComparison.Ordinal))
            {
                throw BusinessException.Conflict(ex.Message);
            }
            throw BusinessException.BadRequest(ex.Message);
        }
    }
}

