using System.Data;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class SportManagementService(IUnitOfWork unitOfWork) : ISportManagementService
{
    public async Task<IReadOnlyList<ManagedSportResponse>> GetActiveAsync(
        long centerId,
        CancellationToken cancellationToken = default) =>
        await unitOfWork.Context.Sports.AsNoTracking()
            .Where(sport => (sport.CenterId == null || sport.CenterId == centerId) && sport.Status == "Active")
            .OrderBy(sport => sport.Name)
            .Select(sport => new ManagedSportResponse(
                sport.Id, sport.Name, sport.Description, sport.Status, sport.CenterId == null))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ManagedSportResponse>> GetAllAsync(
        long centerId,
        CancellationToken cancellationToken = default) =>
        await unitOfWork.Context.Sports.AsNoTracking()
            .Where(sport => sport.CenterId == null || sport.CenterId == centerId)
            .OrderBy(sport => sport.Name)
            .Select(sport => new ManagedSportResponse(
                sport.Id, sport.Name, sport.Description, sport.Status, sport.CenterId == null))
            .ToListAsync(cancellationToken);

    public async Task<ManagedSportResponse> CreateAsync(
        long actorUserId,
        long centerId,
        SaveSportRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = Validate(request);
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await EnsureNameAvailableAsync(centerId, null, input.Name, cancellationToken);

        var sport = new Sport
        {
            CenterId = centerId,
            Name = input.Name,
            Description = input.Description,
            Status = input.Status
        };
        unitOfWork.Context.Sports.Add(sport);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "SPORT.CREATED", "Sport", sport.Id,
            newValues: new { sport.Name, sport.Description, sport.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(sport);
    }

    public async Task<ManagedSportResponse> UpdateAsync(
        long actorUserId,
        long centerId,
        long sportId,
        SaveSportRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = Validate(request);
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var sport = await unitOfWork.Context.Sports.SingleOrDefaultAsync(
            item => item.Id == sportId && item.CenterId == centerId, cancellationToken)
            ?? throw BusinessFailure("Chỉ được sửa bộ môn riêng của trung tâm này.", 404);
        await EnsureNameAvailableAsync(centerId, sportId, input.Name, cancellationToken);
        if (input.Status != "Active" && await unitOfWork.Context.Classes.AnyAsync(
                item => item.SportId == sportId && item.Status != "Cancelled" && item.Status != "Completed",
                cancellationToken))
        {
            throw BusinessFailure("Bộ môn đang có lớp hoạt động; cần đóng các lớp trước khi ngừng sử dụng.", 409);
        }

        var oldValues = new { sport.Name, sport.Description, sport.Status };
        sport.Name = input.Name;
        sport.Description = input.Description;
        sport.Status = input.Status;
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "SPORT.UPDATED", "Sport", sport.Id,
            oldValues, new { sport.Name, sport.Description, sport.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(sport);
    }

    private async Task EnsureNameAvailableAsync(long centerId, long? sportId, string name, CancellationToken ct)
    {
        if (await unitOfWork.Context.Sports.AnyAsync(sport => sport.Id != sportId
                && (sport.CenterId == centerId || sport.CenterId == null) && sport.Name == name, ct))
        {
            throw BusinessFailure("Tên bộ môn đã tồn tại trong danh mục của trung tâm.", 409);
        }
    }

    private static SaveSportRequest Validate(SaveSportRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (name.Length is < 1 or > 100 || description?.Length > 500
            || request.Status is not ("Active" or "Inactive"))
        {
            throw BusinessFailure(
                "Tên bộ môn bắt buộc, tối đa 100 ký tự; mô tả tối đa 500; trạng thái phải là Active hoặc Inactive.", 400);
        }
        return request with { Name = name, Description = description };
    }

    private static InvalidOperationException BusinessFailure(string message, int statusCode) =>
        new(message) { Data = { ["StatusCode"] = statusCode } };

    private static ManagedSportResponse Map(Sport sport) =>
        new(sport.Id, sport.Name, sport.Description, sport.Status, sport.CenterId == null);
}
