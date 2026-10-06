using Microsoft.AspNetCore.Authorization;

namespace SportsCenterManagement.API.Authorization;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
