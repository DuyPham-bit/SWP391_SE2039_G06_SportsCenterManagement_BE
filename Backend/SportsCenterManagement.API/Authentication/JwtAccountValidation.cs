using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using AuthenticatedUser = SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.API.Authentication;

public sealed class JwtAccountValidation(IUnitOfWork unitOfWork) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            context.Fail("A valid user identifier is required.");
            return;
        }

        var cancellationToken = context.HttpContext.RequestAborted;
        var user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken);
        if (user is null || user.Status != "Active" || user.LockedUntil > DateTime.UtcNow)
        {
            context.Fail("Account is inactive, locked, or no longer exists.");
            return;
        }
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken);
        if (role is null)
        {
            context.Fail("Account role is no longer valid.");
            return;
        }
        var centerId = await unitOfWork.Repository<StaffProfile>()
            .Find(profile => profile.UserId == userId && profile.Status == "Active")
            .Select(profile => (long?)profile.CenterId)
            .SingleOrDefaultAsync(cancellationToken);

        // Rebuild identity from DB so signed but stale role/center claims cannot retain revoked access.
        var authenticated = new AuthenticatedUser(user.Id, user.Username, role.Name, centerId, user.Email);
        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
            BearerTokenIssuer.CreateIdentityClaims(authenticated), context.Scheme.Name,
            ClaimTypes.Name, ClaimTypes.Role));
    }
}
