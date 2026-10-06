using AuthRequests = SportsCenterManagement.BLL.DTOs.Auth.Requests;
using AuthResponses = SportsCenterManagement.BLL.DTOs.Auth.Responses;
using MembersRequests = SportsCenterManagement.BLL.DTOs.Members.Requests;
using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IAuthService
{
    Task<MembersResponses.MemberProfileResponse> RegisterAsync(
        AuthRequests.RegisterRequest request,
        long? centerId = null,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResponses.AuthenticatedUser> LoginAsync(AuthRequests.LoginRequest request, CancellationToken cancellationToken = default);
}
