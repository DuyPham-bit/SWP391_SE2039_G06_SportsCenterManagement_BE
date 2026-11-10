using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Members.Activation;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;
using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Counter quick-create flow: the receptionist creates a Member whose initial password is the phone number,
/// then the member verifies the email and sets a personal password through a single-use 24-hour link.
/// </summary>
public sealed partial class MemberActivationService(IUnitOfWork unitOfWork) : IMemberActivationService
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const string InvalidLinkMessage = "Liên kết xác nhận không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quầy lễ tân để được gửi lại email.";

    public async Task<ActivationIssue> QuickCreateAsync(
        long actorUserId,
        QuickCreateMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ValidationException("Họ và tên không được để trống.");
        var phone = NormalizeVietnamesePhone(request.PhoneNumber)
            ?? throw new ValidationException("Số điện thoại phải là số di động Việt Nam gồm 10 chữ số (ví dụ 0912345678).");
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!new EmailAddressAttribute().IsValid(email))
            throw new ValidationException("Địa chỉ email không hợp lệ.");
        if (request.Gender is not null && request.Gender is not ("Male" or "Female" or "Other"))
            throw new ValidationException("Giới tính không hợp lệ.");
        if (request.DateOfBirth > VietnamTime.Today)
            throw new ValidationException("Ngày sinh không được ở tương lai.");

        var centerId = await EnsureCounterStaffAsync(actorUserId, cancellationToken);
        var role = await unitOfWork.Context.Roles.SingleOrDefaultAsync(item => item.Name == "Member", cancellationToken)
            ?? throw new InvalidOperationException("Chưa cấu hình vai trò Member.");

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var users = unitOfWork.Context.Users;
        // Staff are authenticated, so precise duplicate messages are allowed here (unlike self-registration).
        if (await users.AnyAsync(user => user.Phone == phone || user.Username == phone, cancellationToken))
            throw new InvalidOperationException("Số điện thoại đã được sử dụng cho một tài khoản khác.");
        if (await users.AnyAsync(user => user.Email == email, cancellationToken))
            throw new InvalidOperationException("Email đã được sử dụng cho một tài khoản khác.");

        var now = DateTime.UtcNow;
        var user = new User
        {
            // The phone doubles as the login name so members can sign in with "phone + phone" before activation.
            Username = phone,
            Email = email,
            Phone = phone,
            PasswordHash = PasswordHashing.Hash(phone),
            RoleId = role.Id,
            Status = "Active",
            IsEmailVerified = false,
            MustChangePassword = true,
            CreatedAt = now
        };
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var profile = new MemberProfile
        {
            UserId = user.Id,
            CenterId = centerId,
            MemberCode = $"MB{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            FullName = fullName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            CreatedAt = now
        };
        unitOfWork.Context.MemberProfiles.Add(profile);
        var (rawToken, token) = NewToken(user.Id, now);
        unitOfWork.Context.AccountActivationTokens.Add(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "member.created", "MemberProfile", profile.Id,
            newValues: new { UserId = user.Id, profile.MemberCode, RegistrationSource = "counter-quick-create", DefaultPassword = "phone" });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ActivationIssue(Map(profile, user), user.Id, rawToken, token.ExpiresAt);
    }

    public async Task<ActivationIssue> ReissueAsync(long actorUserId, long memberId, CancellationToken cancellationToken = default)
    {
        var centerId = await EnsureCounterStaffAsync(actorUserId, cancellationToken);
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var profile = await unitOfWork.Context.MemberProfiles
            .SingleOrDefaultAsync(item => item.Id == memberId && item.CenterId == centerId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hội viên tại trung tâm này.");
        var user = await unitOfWork.Context.Users.SingleAsync(item => item.Id == profile.UserId, cancellationToken);
        if (user.Status != "Active")
            throw new InvalidOperationException("Tài khoản hội viên đang bị khóa, không thể gửi lại email.");
        if (user.IsEmailVerified)
            throw new InvalidOperationException("Hội viên đã xác nhận email.");

        var now = DateTime.UtcNow;
        var latest = await unitOfWork.Context.AccountActivationTokens
            .Where(item => item.UserId == user.Id && item.UsedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => (DateTime?)item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is { } issuedAt && issuedAt > now - ResendCooldown)
        {
            var wait = (int)Math.Ceiling((issuedAt + ResendCooldown - now).TotalSeconds);
            throw new BusinessException(System.Net.HttpStatusCode.TooManyRequests,
                $"Vui lòng chờ {wait} giây trước khi gửi lại email.", "RESEND_COOLDOWN", wait.ToString());
        }

        // Only the newest link may work.
        await unitOfWork.Context.AccountActivationTokens
            .Where(item => item.UserId == user.Id && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);
        var (rawToken, token) = NewToken(user.Id, now);
        unitOfWork.Context.AccountActivationTokens.Add(token);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "member.activation.resent", "User", user.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ActivationIssue(Map(profile, user), user.Id, rawToken, token.ExpiresAt);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(rawToken);
        await unitOfWork.Context.AccountActivationTokens
            .Where(item => item.TokenHash == hash && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task<ActivationPreviewResponse> PreviewAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var (token, user) = await FindUsableAsync(rawToken, cancellationToken);
        var fullName = await unitOfWork.Context.MemberProfiles
            .Where(item => item.UserId == user.Id)
            .Select(item => item.FullName)
            .SingleOrDefaultAsync(cancellationToken) ?? user.Email;
        return new ActivationPreviewResponse(fullName, user.Email, user.Phone, token.ExpiresAt);
    }

    public async Task CompleteAsync(VerifyAndChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var (token, user) = await FindUsableAsync(request.Token, cancellationToken);
        ValidateNewPassword(request.NewPassword, request.ConfirmPassword, user);

        var now = DateTime.UtcNow;
        // Conditional consume: a concurrent second submission of the same link cannot succeed.
        var consumed = await unitOfWork.Context.AccountActivationTokens
            .Where(item => item.Id == token.Id && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);
        if (consumed != 1)
            throw new ValidationException(InvalidLinkMessage);
        await unitOfWork.Context.AccountActivationTokens
            .Where(item => item.UserId == user.Id && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);

        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.IsEmailVerified = true;
        user.MustChangePassword = false;
        user.Status = "Active";
        user.AuthVersion += 1; // revokes any session opened with the default password
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = now;
        var profile = await unitOfWork.Context.MemberProfiles
            .Where(item => item.UserId == user.Id)
            .Select(item => new { item.Id, item.CenterId, item.FullName, item.MemberCode })
            .SingleOrDefaultAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, user.Id, profile?.CenterId, "member.activation.completed", "User", user.Id,
            newValues: new { EmailVerified = true, PasswordChanged = true });
        if (profile is not null)
            await NotifyCounterStaffAsync(user.Id, profile.Id, profile.CenterId, profile.FullName, profile.MemberCode, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Live status for the receptionist's pending list (scoped to the actor's center).</summary>
    public async Task<IReadOnlyList<MemberVerificationStatus>> GetVerificationStatusAsync(
        long actorUserId, IReadOnlyCollection<long> memberIds, CancellationToken cancellationToken = default)
    {
        if (memberIds.Count == 0) return [];
        if (memberIds.Count > 50) throw new ValidationException("Chỉ kiểm tra tối đa 50 hội viên mỗi lần.");
        var centerId = await EnsureCounterStaffAsync(actorUserId, cancellationToken);
        var ids = memberIds.Distinct().ToList();
        return await (
            from profile in unitOfWork.Context.MemberProfiles.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on profile.UserId equals user.Id
            where ids.Contains(profile.Id) && profile.CenterId == centerId
            select new MemberVerificationStatus(profile.Id, profile.FullName, profile.MemberCode, user.IsEmailVerified))
            .ToListAsync(cancellationToken);
    }

    // Tell the receptionist(s) who created the account or re-sent its link that the member finished activation.
    private async Task NotifyCounterStaffAsync(
        long memberUserId, long profileId, long? centerId, string fullName, string memberCode, CancellationToken cancellationToken)
    {
        var context = unitOfWork.Context;
        var actorIds = await context.AuditLogs.AsNoTracking()
            .Where(log => (log.Action == "member.created" && log.EntityType == "MemberProfile" && log.EntityId == profileId)
                || (log.Action == "member.activation.resent" && log.EntityType == "User" && log.EntityId == memberUserId))
            .Where(log => log.UserId != null)
            .Select(log => log.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var recipients = await context.StaffProfiles.AsNoTracking()
            .Where(staff => actorIds.Contains(staff.UserId) && staff.Status == "Active" && staff.CenterId == centerId)
            .Select(staff => staff.UserId)
            .ToListAsync(cancellationToken);
        await FlowNotificationWriter.CreateAsync(context, memberUserId, recipients,
            "Hội viên đã kích hoạt tài khoản",
            $"{fullName} ({memberCode}) đã xác nhận email và đặt mật khẩu mới. Tài khoản sẵn sàng sử dụng.",
            "MemberActivated", cancellationToken);
    }

    public async Task<User> ChangeInitialPasswordAsync(
        long userId,
        InitialPasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var user = await unitOfWork.Context.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Tài khoản không tồn tại.");
        if (user.Status != "Active")
            throw new UnauthorizedAccessException("Tài khoản không hoạt động.");
        if (!user.MustChangePassword)
            throw new InvalidOperationException("Tài khoản không yêu cầu đổi mật khẩu ban đầu.");
        if (!PasswordHashing.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
            throw new ValidationException("Mật khẩu hiện tại không đúng.");
        ValidateNewPassword(request.NewPassword, request.ConfirmPassword, user);

        var now = DateTime.UtcNow;
        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.AuthVersion += 1;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = now;
        // The email stays unverified: knowing the phone number does not prove mailbox ownership.
        var centerId = await unitOfWork.Context.MemberProfiles
            .Where(item => item.UserId == user.Id).Select(item => item.CenterId).SingleOrDefaultAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, user.Id, centerId, "member.initial_password.changed", "User", user.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    /// <summary>Accepts 0xxxxxxxxx or +84xxxxxxxxx mobile numbers and returns the 10-digit 0-prefixed form.</summary>
    public static string? NormalizeVietnamesePhone(string? value)
    {
        // Country-code handling lives in the shared normalizer so every flow stores the same form.
        var phone = PhoneNumberNormalization.Normalize(value);
        return phone is not null && VietnameseMobile().IsMatch(phone) ? phone : null;
    }

    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken ?? string.Empty)));

    private static (string Raw, AccountActivationToken Entity) NewToken(long userId, DateTime now)
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (raw, new AccountActivationToken
        {
            UserId = userId,
            TokenHash = HashToken(raw),
            CreatedAt = now,
            ExpiresAt = now.Add(LinkLifetime)
        });
    }

    private async Task<(AccountActivationToken Token, User User)> FindUsableAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 128)
            throw new ValidationException(InvalidLinkMessage);
        var hash = HashToken(rawToken.Trim());
        var now = DateTime.UtcNow;
        var token = await unitOfWork.Context.AccountActivationTokens
            .SingleOrDefaultAsync(item => item.TokenHash == hash && item.UsedAt == null && item.ExpiresAt > now, cancellationToken)
            ?? throw new ValidationException(InvalidLinkMessage);
        var user = await unitOfWork.Context.Users.SingleOrDefaultAsync(item => item.Id == token.UserId, cancellationToken);
        if (user is null || user.Status != "Active")
            throw new ValidationException(InvalidLinkMessage);
        return (token, user);
    }

    private static void ValidateNewPassword(string newPassword, string confirmPassword, User user)
    {
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            throw new ValidationException("Mật khẩu xác nhận không khớp.");
        if (PasswordHashing.Verify(newPassword, user.PasswordHash))
            throw new ValidationException("Mật khẩu mới phải khác mật khẩu mặc định ban đầu.");
        PasswordPolicy.ValidateOrThrow(newPassword, user.Email, user.Phone);
    }

    private async Task<long> EnsureCounterStaffAsync(long actorUserId, CancellationToken cancellationToken)
    {
        var actor = await (
            from user in unitOfWork.Context.Users.AsNoTracking()
            join role in unitOfWork.Context.Roles.AsNoTracking() on user.RoleId equals role.Id
            where user.Id == actorUserId
            select new { user.Status, user.LockedUntil, Role = role.Name }).SingleOrDefaultAsync(cancellationToken);
        if (actor is null || actor.Status != "Active" || actor.LockedUntil > DateTime.UtcNow
            || actor.Role is not ("Receptionist" or "Manager"))
            throw new UnauthorizedAccessException("Chỉ Lễ tân hoặc Quản lý được tạo hội viên tại quầy.");

        var centerId = await (
            from staff in unitOfWork.Context.StaffProfiles.AsNoTracking()
            join center in unitOfWork.Context.Centers.AsNoTracking() on staff.CenterId equals center.Id
            where staff.UserId == actorUserId && staff.Status == "Active" && center.Status == "Active"
            select (long?)staff.CenterId).SingleOrDefaultAsync(cancellationToken);
        return centerId ?? throw new UnauthorizedAccessException("Tài khoản không được gán vào trung tâm đang hoạt động.");
    }

    private static MembersResponses.MemberProfileResponse Map(MemberProfile profile, User user) =>
        new(profile.Id, user.Id, profile.MemberCode, user.Username, user.Email, user.Phone, profile.FullName,
            profile.DateOfBirth, profile.Gender, profile.Address, profile.CreatedAt, user.Status,
            IsEmailVerified: user.IsEmailVerified);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("^0[35789][0-9]{8}$")]
    private static partial Regex VietnameseMobile();
}
