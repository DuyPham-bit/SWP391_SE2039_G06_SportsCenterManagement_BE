using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.AccessControl
{
    public static class Requests
    {
        public sealed record ReplaceRolePermissionsRequest(
            [property: Required] IReadOnlyList<string> PermissionCodes);
    }
}
