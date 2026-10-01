using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IAuthService
{
    Task<MemberProfileResponse> RegisterAsync(
        RegisterRequest request,
        long? centerId = null,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUser> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
