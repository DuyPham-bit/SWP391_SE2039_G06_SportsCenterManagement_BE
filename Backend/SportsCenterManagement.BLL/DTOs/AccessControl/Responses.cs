namespace SportsCenterManagement.BLL.DTOs.AccessControl;

public static class Responses
{
    public sealed record PermissionEntry(string Code, string Name, string? Description);

    public sealed record RolePermissionMatrixResponse(
        long RoleId,
        string RoleName,
        string? RoleDescription,
        IReadOnlyList<PermissionEntry> Permissions);
}
