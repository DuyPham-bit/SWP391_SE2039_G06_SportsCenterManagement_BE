namespace SportsCenterManagement.BLL.DTOs.Roles
{
public sealed record RoleResponse(
    long Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    int UserCount,
    int PermissionCount);

public sealed record PermissionResponse(
    long Id,
    string Code,
    string Name,
    string? Description,
    string? Module = null);

public sealed record RolePermissionsResponse(
    long RoleId,
    string RoleName,
    IReadOnlyList<PermissionResponse> Permissions);
}
