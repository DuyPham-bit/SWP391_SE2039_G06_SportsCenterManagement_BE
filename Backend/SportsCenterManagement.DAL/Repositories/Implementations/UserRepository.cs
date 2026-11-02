using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.DAL.Repositories.Implementations;

public sealed class UserRepository(SportsCenterDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(long userId, CancellationToken ct = default)
    {
        return await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
    }

    public async Task<bool> IsUserActiveAsync(long userId, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(user => user.Id == userId
            && user.Status == "Active" && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow),
            ct);
    }

    public async Task<bool> IsStaffActiveAtCenterAsync(long userId, long centerId, CancellationToken ct = default)
    {
        return await db.StaffProfiles.AnyAsync(staff =>
            staff.UserId == userId && staff.CenterId == centerId && staff.Status == "Active",
            ct);
    }

    public async Task<string?> GetRoleNameAsync(long roleId, CancellationToken ct = default)
    {
        return await db.Roles
            .Where(r => r.Id == roleId)
            .Select(r => r.Name)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<MemberProfile?> GetMemberProfileAsync(long userId, CancellationToken ct = default)
    {
        return await db.MemberProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task<CoachProfile?> GetCoachProfileAsync(long userId, CancellationToken ct = default)
    {
        return await db.CoachProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task<StaffProfile?> GetStaffProfileAsync(long userId, CancellationToken ct = default)
    {
        return await db.StaffProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task<bool> ExistsPhoneAsync(long excludeUserId, string phone, CancellationToken ct = default)
    {
        return await db.Users
            .AnyAsync(u => u.Id != excludeUserId && u.Phone == phone, ct);
    }

    public async Task AddAuditLogAsync(AuditLog log, CancellationToken ct = default)
    {
        await db.AuditLogs.AddAsync(log, ct);
    }
}
