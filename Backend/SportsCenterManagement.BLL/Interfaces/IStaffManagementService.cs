using StaffRequests = SportsCenterManagement.BLL.DTOs.Staff.Requests;
using StaffResponses = SportsCenterManagement.BLL.DTOs.Staff.Responses;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IStaffManagementService
{
    Task<StaffResponses.CenterStaffResponse[]> SearchAtCenterAsync(
        long actorUserId,
        long centerId,
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<StaffResponses.CenterStaffResponse> CreateAtCenterAsync(
        long actorUserId,
        long centerId,
        StaffRequests.CreateCenterStaffRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffResponses.CenterStaffResponse> UpdateAtCenterAsync(
        long actorUserId,
        long centerId,
        long staffUserId,
        StaffRequests.UpdateCenterStaffRequest request,
        CancellationToken cancellationToken = default);
}
