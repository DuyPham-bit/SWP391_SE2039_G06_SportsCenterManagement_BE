using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.CoachProfessional;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
public sealed class CoachProfessionalController(CoachProfessionalService service) : FlowControllerBase
{
    [HttpGet("coaches/me/professional")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> CoachWorkspace([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await service.CoachWorkspace(CurrentUserId, from, to, ct));

    [HttpGet("coaches/me/leave-replacement-options")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> LeaveReplacementOptions(
        [FromQuery] DateTime startAt, [FromQuery] DateTime endAt, CancellationToken ct) =>
        Ok(await service.GetLeaveReplacementOptions(CurrentUserId, startAt, endAt, ct));

    [HttpGet("coaches/me/kpi")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> Kpi([FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        Ok(await service.GetKpi(CurrentUserId, year, month, ct));

    [HttpPost("coaches/me/work-events")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> CreateEvent(SaveWorkEventRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.CreateWorkEvent(CurrentUserId, request, ct) });

    [HttpPatch("coaches/me/work-events/{id:long}/status")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> ChangeEvent(long id, ChangeWorkEventStatusRequest request, CancellationToken ct)
    { await service.ChangeWorkEvent(CurrentUserId, id, request, false, ct); return NoContent(); }

    [HttpPost("coaches/me/leave-requests")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> RequestLeave(SaveLeaveRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.RequestLeave(CurrentUserId, request, ct) });

    [HttpPost("coaches/me/member-health-records")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> Health(SaveHealthRecordRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SaveHealthRecord(CurrentUserId, request, ct) });

    [HttpPost("coaches/me/exercises")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> Exercise(SaveExerciseRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SaveExercise(CurrentUserId, request, ct) });

    [HttpPost("coaches/me/homework")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> Homework(SaveHomeworkRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SaveHomework(CurrentUserId, request, ct) });

    [HttpPatch("coaches/me/homework-submissions/{id:long}")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> ReviewHomework(long id, ReviewHomeworkRequest request, CancellationToken ct)
    { await service.ReviewHomework(CurrentUserId, id, request, ct); return NoContent(); }

    [HttpPost("coaches/me/nutrition-plans")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> Nutrition(SaveNutritionPlanRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SaveNutrition(CurrentUserId, request, ct) });

    [HttpGet("coaches/me/members/{memberId:long}/messages")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> CoachMessages(long memberId, [FromQuery] long coachId, CancellationToken ct) =>
        Ok(await service.Messages(CurrentUserId, coachId, memberId, ct));

    [HttpPost("coaches/me/messages")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> CoachSendMessage(SendCoachMessageRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SendMessage(CurrentUserId, request, ct) });

    [HttpPost("coaches/me/broadcasts")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> Broadcast(SendBroadcastRequest request, CancellationToken ct) =>
        Ok(await service.Broadcast(CurrentUserId, request, ct));

    [HttpPost("coaches/me/resource-bookings")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> BookResource(BookResourceRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.BookResource(CurrentUserId, request, ct) });

    [HttpDelete("coaches/me/resource-bookings/{id:long}")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> CancelResource(long id, CancellationToken ct)
    { await service.CancelResourceBooking(CurrentUserId, id, ct); return NoContent(); }

    [HttpPost("coaches/me/equipment-issues")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("manage_coach_workspace")]
    public async Task<IActionResult> ReportIssue(ReportEquipmentIssueRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.ReportIssue(CurrentUserId, request, ct) });

    [HttpGet("members/me/coach-portal")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("use_coach_portal")]
    public async Task<IActionResult> MemberPortal(CancellationToken ct) => Ok(await service.MemberWorkspace(CurrentUserId, ct));

    [HttpPatch("members/me/coach-events/{id:long}/status")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("use_coach_portal")]
    public async Task<IActionResult> RespondEvent(long id, ChangeWorkEventStatusRequest request, CancellationToken ct)
    { await service.ChangeWorkEvent(CurrentUserId, id, request, true, ct); return NoContent(); }

    [HttpPost("members/me/homework/{id:long}/submission")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("use_coach_portal")]
    public async Task<IActionResult> SubmitHomework(long id, SubmitHomeworkRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SubmitHomework(CurrentUserId, id, request, ct) });

    [HttpPost("members/me/food-diary")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("use_coach_portal")]
    public async Task<IActionResult> FoodDiary(SaveFoodDiaryRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.AddFoodDiary(CurrentUserId, request, ct) });

    [HttpPost("members/me/coach-reviews")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("use_coach_portal")]
    public async Task<IActionResult> ReviewCoach(SaveCoachReviewRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.ReviewCoach(CurrentUserId, request, ct) });

    [HttpGet("members/me/coaches/{coachId:long}/messages")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("use_coach_portal")]
    public async Task<IActionResult> MemberMessages(long coachId, [FromQuery] long memberId, CancellationToken ct) =>
        Ok(await service.Messages(CurrentUserId, coachId, memberId, ct));

    [HttpPost("members/me/coach-messages")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("use_coach_portal")]
    public async Task<IActionResult> MemberSendMessage(SendCoachMessageRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SendMessage(CurrentUserId, request, ct) });

    [HttpGet("manager/coach-operations")]
    [Authorize(Roles = "MANAGER")]
    [CenterCapability("manage_coach_operations")]
    public async Task<IActionResult> ManagerWorkspace(CancellationToken ct) => Ok(await service.ManagerWorkspace(CurrentUserId, ct));

    [HttpPatch("manager/coach-operations/leaves/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    [CenterCapability("manage_coach_operations")]
    public async Task<IActionResult> ReviewLeave(long id, ReviewLeaveRequest request, CancellationToken ct)
    { await service.ReviewLeave(CurrentUserId, id, request, ct); return NoContent(); }

    [HttpPost("manager/coach-operations/compensation")]
    [Authorize(Roles = "MANAGER")]
    [CenterCapability("manage_coach_operations")]
    public async Task<IActionResult> Compensation(SaveCompensationPolicyRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SaveCompensation(CurrentUserId, request, ct) });

    [HttpPost("manager/coach-operations/sales-credits")]
    [Authorize(Roles = "MANAGER")]
    [CenterCapability("manage_coach_operations")]
    public async Task<IActionResult> SalesCredit(SaveSalesCreditRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.SaveSalesCredit(CurrentUserId, request, ct) });

    [HttpPost("manager/coach-operations/resources")]
    [Authorize(Roles = "MANAGER")]
    [CenterCapability("manage_coach_operations")]
    public async Task<IActionResult> Resource(CreateResourceRequest request, CancellationToken ct) =>
        Ok(new { Id = await service.CreateResource(CurrentUserId, request, ct) });

    [HttpPatch("manager/coach-operations/equipment-issues/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    [CenterCapability("manage_coach_operations")]
    public async Task<IActionResult> ResolveIssue(long id, ResolveEquipmentIssueRequest request, CancellationToken ct)
    { await service.ResolveIssue(CurrentUserId, id, request, ct); return NoContent(); }
}
