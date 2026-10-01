using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, BearerTokenIssuer tokenIssuer) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await authService.RegisterAsync(request, cancellationToken: cancellationToken);
            return Created("/api/members/me", profile);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Username, email, or phone number is already in use." });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await authService.LoginAsync(request, cancellationToken);
            var (token, expiresAt) = tokenIssuer.Issue(user);
            return Ok(new LoginResponse(token, expiresAt, user.Username, user.Role));
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { message = "Invalid username or password, or the account is currently locked." });
        }
    }
}
