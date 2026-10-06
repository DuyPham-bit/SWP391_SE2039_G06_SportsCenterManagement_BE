using Microsoft.AspNetCore.Authorization;

namespace SportsCenterManagement.API.Authorization;

public sealed class RequirePermissionAttribute(string permissionCode)
    : AuthorizeAttribute($"Permission:{permissionCode}");
