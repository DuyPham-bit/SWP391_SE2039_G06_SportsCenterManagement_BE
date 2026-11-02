using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long userId, CancellationToken ct = default);
    Task<bool> IsUserActiveAsync(long userId, CancellationToken ct = default);
    Task<bool> IsStaffActiveAtCenterAsync(long userId, long centerId, CancellationToken ct = default);
    Task<string?> GetRoleNameAsync(long roleId, CancellationToken ct = default);
    Task<MemberProfile?> GetMemberProfileAsync(long userId, CancellationToken ct = default);
    Task<CoachProfile?> GetCoachProfileAsync(long userId, CancellationToken ct = default);
    Task<StaffProfile?> GetStaffProfileAsync(long userId, CancellationToken ct = default);
    Task<bool> ExistsPhoneAsync(long excludeUserId, string phone, CancellationToken ct = default);
    Task AddAuditLogAsync(AuditLog log, CancellationToken ct = default);
}
