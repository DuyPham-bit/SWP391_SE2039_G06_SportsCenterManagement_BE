using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class AuthService(IUnitOfWork unitOfWork) : IAuthService
{
    public async Task<MemberProfileResponse> RegisterAsync(
        RegisterRequest request,
        long? centerId = null,
        CancellationToken cancellationToken = default)
    {
        // Validate password against security rules before processing
        ValidatePassword(request.Password);
        centerId ??= request.CenterId;

        // Normalize to detect duplicates regardless of letter casing or whitespace
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        // Validate username format (letters, numbers, dot, underscore, hyphen)
        if (username.Length is < 3 or > 100 || !Regex.IsMatch(username, @"^[a-z0-9._-]+$"))
        {
            throw new ValidationException("Username can only contain letters, numbers, dots, underscores, or hyphens.");
        }

        // Validate email format
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new ValidationException("Invalid email format.");
        }

        // Normalize and validate phone number
        var phone = NormalizeOptional(request.Phone);
        ValidatePhone(phone);

        // Validate date of birth cannot be in the future
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException("Date of birth cannot be in the future.");
        }

        var users = unitOfWork.Repository<User>();

        // Ensure username or email is not already registered
        if (await users.AnyAsync(user => user.Username == username || user.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Username or email is already in use.");
        }

        // Ensure phone number is not already registered if provided
        if (phone is not null && await users.AnyAsync(user => user.Phone == phone, cancellationToken))
        {
            throw new InvalidOperationException("Phone number is already in use.");
        }

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
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

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

        // Commit transaction upon successful persistence
        await transaction.CommitAsync(cancellationToken);

        return new MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
            user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
            profile.Address, profile.CreatedAt);
    }

    public async Task<AuthenticatedUser> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Normalize login identifier (username or email)
        var login = request.Login.Trim().ToLowerInvariant();

        // Search for user by either username or email
        var user = await unitOfWork.Repository<User>()
            .Find(item => item.Username == login || item.Email == login)
            .SingleOrDefaultAsync(cancellationToken);

        // If user is not found, execute dummy hash verification to protect against timing attacks
        if (user is null)
        {
            _ = PasswordHashing.Verify(request.Password, DummyPasswordHash.Value);
            throw new InvalidOperationException("Invalid username or password.");
        }

        var now = DateTime.UtcNow;

        // Check if account is inactive or currently locked out
        if (user.Status != "Active" || user.LockedUntil > now)
        {
            throw new InvalidOperationException("Account is currently unavailable for login.");
        }

        // Verify the entered password against the stored password hash
        if (!PasswordHashing.Verify(request.Password, user.PasswordHash))
        {
            // Increment failed attempt counter and lock account for 15 minutes after 5 consecutive failures
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.FailedLoginAttempts = 0;
                user.LockedUntil = now.AddMinutes(15);
            }
            unitOfWork.Repository<User>().Update(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid username or password.");
        }

        // Reset failed login counter and update login timestamp on successful authentication
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        unitOfWork.Repository<User>().Update(user);

        // Fetch user role name
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken)
            ?? throw new InvalidOperationException("User role is no longer valid.");

        // Fetch associated center ID if user is an active staff member
        var centerId = await unitOfWork.Repository<StaffProfile>()
            .Find(profile => profile.UserId == user.Id && profile.Status == "Active")
            .Select(profile => (long?)profile.CenterId)
            .SingleOrDefaultAsync(cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthenticatedUser(user.Id, user.Username, role.Name, centerId);
    }

    /// <summary>
    /// Validates password complexity: minimum 12 characters, including uppercase, lowercase, numbers, and special characters.
    /// </summary>
    /// <param name="password">Plain text password to be validated.</param>
    private static void ValidatePassword(string password)
    {
        // 1. Kiểm tra chuỗi rỗng hoặc độ dài tối thiểu 12 ký tự / Check null or minimum length of 12 characters
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12
            // 2. Phải chứa ít nhất 1 chữ hoa / Must contain at least one uppercase letter (A-Z)
            || !Regex.IsMatch(password, "[A-Z]")
            // 3. Phải chứa ít nhất 1 chữ thường / Must contain at least one lowercase letter (a-z)
            || !Regex.IsMatch(password, "[a-z]")
            // 4. Phải chứa ít nhất 1 chữ số / Must contain at least one number (0-9)
            || !Regex.IsMatch(password, "[0-9]")
            // 5. Phải chứa ít nhất 1 ký tự đặc biệt / Must contain at least one special character (non-alphanumeric)
            || !Regex.IsMatch(password, "[^a-zA-Z0-9]"))
        {
            // Ném ngoại lệ nếu không thỏa mãn bất kỳ điều kiện bảo mật nào / Throw validation exception if any rule fails
            throw new ValidationException("Password must be at least 12 characters long and contain uppercase letters, lowercase letters, numbers, and special characters.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
