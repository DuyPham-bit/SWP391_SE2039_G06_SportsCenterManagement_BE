using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Roles
{
    public sealed record UpdateRolePermissionsRequest
    {
        [Required]
        public required List<long> PermissionIds { get; init; } = [];
    }

    public sealed record CreateRoleRequest
    {
        [Required, MaxLength(50)]
        public required string Name { get; init; }

        [MaxLength(255)]
        public string? Description { get; init; }
    }

    public sealed record UpdateRoleRequest
    {
        [Required, MaxLength(50)]
        public required string Name { get; init; }

        [MaxLength(255)]
        public string? Description { get; init; }
    }
}
