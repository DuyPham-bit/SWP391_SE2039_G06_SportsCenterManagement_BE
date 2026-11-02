using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Payroll;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
public sealed class PayrollController(PayrollService service) : FlowControllerBase
{
    [HttpGet("payroll/me")]
    [Authorize(Roles = "COACH,RECEPTIONIST,MANAGER,ADMIN")]
    public async Task<ActionResult<PayrollRow>> Mine([FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        Ok(await service.GetMine(CurrentUserId, year, month, ct));

    [HttpGet("payroll/report")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<PayrollReport>> Report([FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        Ok(await service.GetReport(CurrentUserId, year, month, ct));

    [HttpPost("payroll/staff-policy")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<IActionResult> SavePolicy(SaveStaffCompensationRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SaveStaffPolicy(CurrentUserId, request, ct) });
}
