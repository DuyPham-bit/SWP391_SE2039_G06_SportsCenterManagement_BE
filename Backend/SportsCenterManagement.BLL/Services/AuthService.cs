using SportsCenterManagement.BLL.Common;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using AuthRequests = SportsCenterManagement.BLL.DTOs.Auth.Requests;
using AuthResponses = SportsCenterManagement.BLL.DTOs.Auth.Responses;
using MembersRequests = SportsCenterManagement.BLL.DTOs.Members.Requests;
using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class AuthService(IUnitOfWork unitOfWork) : IAuthService
{
    public async Task<MembersResponses.MemberProfileResponse> RegisterAsync(
        AuthRequests.RegisterRequest request,
        long? centerId = null,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException("Full name is required.");
        }
        centerId ??= request.CenterId;

        // Normalize to detect duplicates regardless of letter casing or whitespace
        var requestedUsername = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        // Validate an explicitly chosen username (letters, numbers, dot, underscore, hyphen).
        // Web self-registration sends none: the server derives one below, so the email's local part
        // (which may contain '+' or collide across domains) never blocks a registration.
        if (requestedUsername is not null
            && (requestedUsername.Length is < 3 or > 100 || !Regex.IsMatch(requestedUsername, @"^[a-z0-9._-]+$")))
        {
            throw new ValidationException("Tên đăng nhập chỉ gồm chữ thường không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang (3–100 ký tự).");
        }

        // Validate email format
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new ValidationException("Invalid email format.");
        }

        // Normalize and validate phone number
        var phone = PhoneNumberNormalization.Normalize(request.Phone);
        ValidatePhone(phone);
        PasswordPolicy.ValidateOrThrow(request.Password, email, phone);

        // Validate date of birth cannot be in the future
        if (request.DateOfBirth > VietnamTime.Today)
        {
            throw new ValidationException("Date of birth cannot be in the future.");
        }

        var users = unitOfWork.Repository<User>();

        // Retrieve default 'Member' role from database
        var role = await unitOfWork.Repository<Role>()
            .Find(item => item.Name == "Member")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Member role configuration is missing.");

        // If centerId is specified, verify that the center exists and is active
        if (centerId.HasValue && !await unitOfWork.Repository<Center>()
                .AnyAsync(center => center.Id == centerId.Value && center.Status == "Active", cancellationToken))
        {
            throw new InvalidOperationException("Center does not exist or is inactive.");
        }

        // Begin transaction: User and MemberProfile must succeed or roll back together
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var now = DateTime.UtcNow;

        // Self-registration proves mailbox ownership; staff-created memberships retain their existing workflow.
        if (!actorUserId.HasValue)
        {
            var verification = await unitOfWork.Context.EmailVerificationTokens
                .Where(item => item.Email == email && item.UsedAt == null && item.ExpiresAt > now)
                .OrderByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
            if (verification is null || verification.FailedAttempts >= 5
                || request.EmailOtp is null || !Regex.IsMatch(request.EmailOtp, "^[0-9]{6}$"))
                throw new ValidationException("Vui lòng nhập mã xác thực email hợp lệ. Mã có thể đã hết hạn hoặc đã được sử dụng.");

            if (!BCrypt.Net.BCrypt.Verify(request.EmailOtp, verification.Token))
            {
                verification.FailedAttempts++;
                if (verification.FailedAttempts >= 5) verification.UsedAt = now;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                var remaining = Math.Max(0, 5 - verification.FailedAttempts);
                throw new OtpAttemptException(remaining > 0
                    ? $"Mã xác thực không đúng. Bạn còn {remaining} lần thử."
                    : "Bạn đã nhập sai 5 lần. Mã này đã bị khóa, cần gửi mã mới.", remaining);
            }
            verification.UsedAt = now;
        }

        // Only disclose duplicate account details after mailbox verification (or authenticated staff creation).
        if (await users.AnyAsync(user => user.Email == email, cancellationToken))
            throw new InvalidOperationException("Email đã được sử dụng cho một tài khoản khác.");
        if (phone is not null && await users.AnyAsync(user => user.Phone == phone, cancellationToken))
            throw new InvalidOperationException("Số điện thoại đã được sử dụng cho một tài khoản khác.");
        if (requestedUsername is not null && await users.AnyAsync(user => user.Username == requestedUsername, cancellationToken))
            throw new InvalidOperationException("Tên đăng nhập đã được sử dụng.");
        var username = requestedUsername ?? await GenerateUsernameAsync(email, cancellationToken);

        // Create new user entity with hashed password
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = PasswordHashing.Hash(request.Password),
            Phone = phone,
            RoleId = role.Id,
            Status = "Active",
            CreatedAt = now
        };
        await users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Create member profile linked to the newly created user
        var profile = new MemberProfile
        {
            UserId = user.Id,
            CenterId = centerId,
            MemberCode = $"MB{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            CreatedAt = now
        };
        await unitOfWork.Repository<MemberProfile>().AddAsync(profile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId ?? user.Id, centerId, "member.created", "MemberProfile", profile.Id,
            newValues: new { UserId = user.Id, MemberCode = profile.MemberCode, RegistrationSource = actorUserId.HasValue ? "center" : "self" });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Commit transaction upon successful persistence
        await transaction.CommitAsync(cancellationToken);

        return new MembersResponses.MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
            user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
            profile.Address, profile.CreatedAt, user.Status);
    }

    public async Task<AuthResponses.AuthenticatedUser> LoginAsync(
        AuthRequests.LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Normalize login identifier (username or email)
        var login = request.Login.Trim().ToLowerInvariant();

        // Search by email, then username, then phone (stored in the normalized 0… form, so "+84 912…"
        // also matches). A fixed precedence keeps the result deterministic: these columns are each unique
        // but one account's username could equal another account's phone.
        var users = unitOfWork.Repository<User>();
        var user = await users.Find(item => item.Email == login).SingleOrDefaultAsync(cancellationToken)
            ?? await users.Find(item => item.Username == login).SingleOrDefaultAsync(cancellationToken);
        if (user is null && PhoneNumberNormalization.Normalize(login) is { } phone
            && phone.TrimStart('+').All(char.IsAsciiDigit))
        {
            user = await users.Find(item => item.Phone == phone).SingleOrDefaultAsync(cancellationToken);
        }

        // If user is not found, execute dummy hash verification to protect against timing attacks
        if (user is null)
        {
            _ = PasswordHashing.Verify(request.Password, DummyPasswordHash.Value);
            throw new InvalidOperationException("Invalid username or password.");
        }

        var now = DateTime.UtcNow;

        // Verify the entered password against the stored password hash
        if (!PasswordHashing.Verify(request.Password, user.PasswordHash))
        {
            // Atomic update prevents simultaneous failed logins from losing increments or bypassing the lock threshold.
            await unitOfWork.Context.Users
                .Where(item => item.Id == user.Id && item.PasswordHash == user.PasswordHash && item.Status == "Active"
                    && (!item.LockedUntil.HasValue || item.LockedUntil.Value <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FailedLoginAttempts,
                        item => item.FailedLoginAttempts >= 4 ? 0 : item.FailedLoginAttempts + 1)
                    .SetProperty(item => item.LockedUntil,
                        item => item.FailedLoginAttempts >= 4 ? now.AddMinutes(15) : item.LockedUntil)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            throw new InvalidOperationException("Invalid username or password.");
        }

        // Only disclose an account lock after the caller has proved they know the password.
        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.")
            {
                Data =
                {
                    ["StatusCode"] = 423,
                    ["Code"] = "ACCOUNT_DISABLED"
                }
            };
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > now)
        {
            throw new InvalidOperationException("Tài khoản đang tạm thời bị khóa. Vui lòng thử lại sau.")
            {
                Data =
                {
                    ["StatusCode"] = 423,
                    ["Code"] = "ACCOUNT_TEMPORARILY_LOCKED"
                }
            };
        }

        // Reset failed login counter and update login timestamp on successful authentication
        var updated = await unitOfWork.Context.Users
            .Where(item => item.Id == user.Id && item.PasswordHash == user.PasswordHash && item.Status == "Active"
                && (!item.LockedUntil.HasValue || item.LockedUntil.Value <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FailedLoginAttempts, 0)
                .SetProperty(item => item.LockedUntil, (DateTime?)null)
                .SetProperty(item => item.LastLoginAt, now)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (updated == 0)
        {
            throw new InvalidOperationException("Account is currently unavailable for login.");
        }

        // Fetch user role name
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken)
            ?? throw new InvalidOperationException("User role is no longer valid.");

        // Fetch associated center ID if user is an active staff member
        var centerId = await unitOfWork.Repository<StaffProfile>()
            .Find(profile => profile.UserId == user.Id && profile.Status == "Active")
            .Select(profile => (long?)profile.CenterId)
            .SingleOrDefaultAsync(cancellationToken);

        return new AuthResponses.AuthenticatedUser(user.Id, user.Username, role.Name, centerId, user.Email, user.AuthVersion);
    }

    /// <summary>
    /// Readable, valid and unique login name from the email local part: invalid characters
    /// ('+', accents, …) become '-', and a numeric suffix resolves collisions such as
    /// duy@gmail.com vs duy@yahoo.com. Runs inside the registration transaction.
    /// </summary>
    private async Task<string> GenerateUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var local = email.Split('@')[0].ToLowerInvariant();
        var stem = Regex.Replace(local, "[^a-z0-9._-]+", "-").Trim('-', '.', '_');
        if (stem.Length > 80) stem = stem[..80].TrimEnd('-', '.', '_');
        if (stem.Length < 3) stem = ("user-" + stem).TrimEnd('-');

        var users = unitOfWork.Repository<User>();
        if (!await users.AnyAsync(user => user.Username == stem, cancellationToken)) return stem;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = $"{stem}-{System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 100000)}";
            if (!await users.AnyAsync(user => user.Username == candidate, cancellationToken)) return candidate;
        }
        return $"{stem}-{Guid.NewGuid():N}"[..Math.Min(100, stem.Length + 33)];
    }

    private static void ValidatePhone(string? phone)
    {
        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            throw new ValidationException("Phone number must contain between 8 and 15 digits.");
        }
    }

    private static class DummyPasswordHash
    {
        public static readonly string Value = PasswordHashing.Hash("Dummy-Password-Not-For-Login-123!");
    }
}
