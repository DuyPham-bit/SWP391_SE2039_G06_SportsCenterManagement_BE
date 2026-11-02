using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Profile;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>Profile endpoints for the authenticated user.</summary>
[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var profile = await profileService.GetProfileAsync(userId, cancellationToken);
        if (profile is null) return Unauthorized();

        return Ok(MapToResponse(profile));
    }

    [HttpPatch("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var updateDto = new UpdateUserProfileRequest(request.FullName, request.Phone, request.AvatarKey);
        var updated = await profileService.UpdateProfileAsync(userId, updateDto, cancellationToken);

        return Ok(MapToResponse(updated));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private static ProfileResponse MapToResponse(UserProfileResponse dto) =>
        new(
            dto.UserId,
            dto.Email,
            dto.FullName,
            dto.Phone,
            dto.Role,
            dto.Status,
            dto.MemberCode,
            dto.CreatedAt,
            dto.CenterId,
            dto.Avatar,
            dto.AvatarKey);
}
