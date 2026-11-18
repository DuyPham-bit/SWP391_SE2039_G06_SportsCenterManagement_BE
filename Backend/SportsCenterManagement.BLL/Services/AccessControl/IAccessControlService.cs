using AccessControlRequests = SportsCenterManagement.BLL.DTOs.AccessControl.Requests;
using AccessControlResponses = SportsCenterManagement.BLL.DTOs.AccessControl.Responses;

namespace SportsCenterManagement.BLL.Services.AccessControl;

public interface IAccessControlService
{
    Task<IReadOnlyList<AccessControlResponses.RolePermissionMatrixResponse>> GetRolePermissionMatrixAsync(
        CancellationToken cancellationToken = default);

    Task<AccessControlResponses.RolePermissionMatrixResponse> ReplaceRolePermissionsAsync(
        long actorUserId,
        long roleId,
        AccessControlRequests.ReplaceRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}
