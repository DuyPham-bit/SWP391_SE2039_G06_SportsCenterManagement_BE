using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.DTOs.Auth;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Controllers;

/// <summary>Profile endpoints for the authenticated user.</summary>
[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(SportsCenterDbContext dbContext) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> GetMe(CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();

        return Ok(await BuildResponseAsync(user, cancellationToken));
    }

    [HttpPatch("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();

        var memberProfile = await dbContext.MemberProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);
        var coachProfile = await dbContext.CoachProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);
        var staffProfile = await dbContext.StaffProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);
        if (memberProfile is null && coachProfile is null && staffProfile is null)
        {
            return NotFound(new { message = "Hồ sơ chưa được cấp phát cho tài khoản này." });
        }

        var fullName = request.FullName.Trim();
        var phone = request.Phone is null
            ? user.Phone
            : string.IsNullOrWhiteSpace(request.Phone)
                ? null
                : request.Phone.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);

        if (fullName.Length == 0)
        {
            return BadRequest(new { message = "Họ và tên không được để trống." });
        }

        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            return BadRequest(new { message = "Số điện thoại cần có từ 8 đến 15 chữ số; có thể bắt đầu bằng dấu +." });
        }

        user.Phone = phone;
        if (memberProfile is not null)
        {
            memberProfile.FullName = fullName;
            memberProfile.UpdatedAt = DateTime.UtcNow;
        }

        if (coachProfile is not null)
        {
            coachProfile.FullName = fullName;
            coachProfile.UpdatedAt = DateTime.UtcNow;
        }

        if (staffProfile is not null)
        {
            staffProfile.FullName = fullName;
            staffProfile.UpdatedAt = DateTime.UtcNow;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildResponseAsync(user, cancellationToken));
    }

    private async Task<User?> FindCurrentUserAsync(CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(claim, out var userId)
            ? await dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken)
            : null;
    }

    private async Task<ProfileResponse> BuildResponseAsync(User user, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Where(item => item.Id == user.RoleId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "USER";
        var memberProfile = await dbContext.MemberProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);
        var coachProfile = await dbContext.CoachProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);
        var staffProfile = await dbContext.StaffProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);

        var centerId = staffProfile?.CenterId ?? coachProfile?.CenterId ?? memberProfile?.CenterId;
        var fullName = memberProfile?.FullName ?? coachProfile?.FullName ?? staffProfile?.FullName ?? user.Email;
        return new ProfileResponse(
            user.Id,
            user.Email,
            fullName,
            user.Phone,
            role.ToUpperInvariant(),
            user.Status,
            memberProfile?.MemberCode,
            new DateTimeOffset(user.CreatedAt, TimeSpan.Zero),
            centerId);
    }
}
