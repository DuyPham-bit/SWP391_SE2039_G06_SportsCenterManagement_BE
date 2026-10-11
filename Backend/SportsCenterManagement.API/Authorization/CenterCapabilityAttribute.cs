using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CenterCapabilityAttribute(string capability) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) { context.Result = new UnauthorizedResult(); return; }
        var db = context.HttpContext.RequestServices.GetRequiredService<SportsCenterDbContext>();
        var ct = context.HttpContext.RequestAborted;
        var userId = CenterScope.GetUserId(context.HttpContext.User);
        var row = await (from u in db.Users join r in db.Roles on u.RoleId equals r.Id where u.Id == userId select new { u.RoleId, r.Name }).SingleOrDefaultAsync(ct);
        if (row is null) { context.Result = new ForbidResult(); return; }
        if (row.Name is "Manager" or "SystemAdmin" or "Admin") return;
        var baseline = CenterRolePolicy.Capabilities(row.Name);
        var centerId = await CenterRolePolicy.CenterForUser(db, userId, ct);
        var policy = centerId.HasValue ? await CenterRolePolicy.Read(db, centerId.Value, row.RoleId, ct) : null;
        if (!baseline.Contains(capability) || (policy is not null && !policy.CapabilityIds.Contains(capability)))
            context.Result = new ForbidResult();
    }
}
