using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Profile;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class ProfileService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IObjectStorageClient? storageClient = null,
    ILogger<ProfileService>? logger = null) : IProfileService
{
    public async Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null) return null;

        return await BuildResponseAsync(user, ct);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(
        long userId,
        UpdateUserProfileRequest request,
        CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy người dùng.");

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length == 0)
        {
            throw BusinessException.BadRequest("Họ và tên không được để trống.");
        }

        var avatarKey = request.AvatarKey?.Trim();
        if (avatarKey is not null &&
            (!avatarKey.StartsWith($"avatars/{user.Id}/", StringComparison.Ordinal) ||
             avatarKey.Contains("../", StringComparison.Ordinal)))
        {
            throw BusinessException.BadRequest("Đường dẫn ảnh đại diện không hợp lệ.");
        }

        var phone = request.Phone is null
            ? user.Phone
            : PhoneNumberNormalization.Normalize(request.Phone);

        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            throw BusinessException.BadRequest("Số điện thoại cần có từ 8 đến 15 chữ số; có thể bắt đầu bằng dấu +.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        if (phone is not null && await userRepository.ExistsPhoneAsync(user.Id, phone, ct))
        {
            throw BusinessException.Conflict("Số điện thoại đã được sử dụng.");
        }

        var memberProfile = await userRepository.GetMemberProfileAsync(user.Id, ct);
        var coachProfile = await userRepository.GetCoachProfileAsync(user.Id, ct);
        var staffProfile = await userRepository.GetStaffProfileAsync(user.Id, ct);

        var previousPhone = PhoneNumberNormalization.Normalize(user.Phone);
        var previousPhonePresent = user.Phone is not null;
        var previousFullName = memberProfile?.FullName ?? coachProfile?.FullName ?? staffProfile?.FullName;
        var previousAvatarKey = user.AvatarKey;
        var fullNameChanged = previousFullName != fullName;
        var now = DateTime.UtcNow;
        var centerId = staffProfile?.CenterId ?? coachProfile?.CenterId ?? memberProfile?.CenterId;

        user.Phone = phone;
        user.AvatarKey = avatarKey;
        user.UpdatedAt = now;

        if (memberProfile is not null)
        {
            memberProfile.FullName = fullName;
            memberProfile.UpdatedAt = now;
        }

        if (coachProfile is not null)
        {
            coachProfile.FullName = fullName;
            coachProfile.UpdatedAt = now;
        }

        if (staffProfile is not null)
        {
            staffProfile.FullName = fullName;
            staffProfile.UpdatedAt = now;
        }

        await userRepository.AddAuditLogAsync(new AuditLog
        {
            UserId = user.Id,
            CenterId = centerId,
            Action = "user.profile.updated",
            EntityType = "User",
            EntityId = user.Id,
            OldValues = JsonSerializer.Serialize(new
            {
                PhonePresent = previousPhonePresent,
                FullNamePresent = previousFullName is not null
            }),
            NewValues = JsonSerializer.Serialize(new
            {
                FullNameChanged = fullNameChanged,
                PhoneChanged = previousPhone != PhoneNumberNormalization.Normalize(phone),
                PhonePresent = phone is not null
            }),
            CreatedAt = now
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (storageClient is not null &&
            !string.IsNullOrWhiteSpace(previousAvatarKey) &&
            !string.Equals(previousAvatarKey, avatarKey, StringComparison.Ordinal))
        {
            try
            {
                await storageClient.DeleteAsync(previousAvatarKey, ct);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not delete replaced avatar {AvatarKey}.", previousAvatarKey);
            }
        }

        return await BuildResponseAsync(user, ct);
    }

    private async Task<UserProfileResponse> BuildResponseAsync(User user, CancellationToken ct)
    {
        var role = await userRepository.GetRoleNameAsync(user.RoleId, ct) ?? "USER";
        var memberProfile = await userRepository.GetMemberProfileAsync(user.Id, ct);
        var coachProfile = await userRepository.GetCoachProfileAsync(user.Id, ct);
        var staffProfile = await userRepository.GetStaffProfileAsync(user.Id, ct);

        var centerId = staffProfile?.CenterId ?? coachProfile?.CenterId ?? memberProfile?.CenterId;
        var fullName = memberProfile?.FullName ?? coachProfile?.FullName ?? staffProfile?.FullName ?? user.Email;

        return new UserProfileResponse(
            user.Id,
            user.Email,
            fullName,
            user.Phone,
            role.ToUpperInvariant(),
            user.Status,
            memberProfile?.MemberCode,
            user.CreatedAt,
            centerId,
            user.AvatarKey,
            user.AvatarKey);
    }
}

