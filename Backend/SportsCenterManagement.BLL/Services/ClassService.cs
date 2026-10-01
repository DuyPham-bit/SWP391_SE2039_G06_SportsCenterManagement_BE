using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class ClassService(IUnitOfWork unitOfWork) : IClassService
{
    public async Task<IReadOnlyList<ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Repository<ClassEntity>()
            .Find(item => item.CenterId == centerId && item.Status == "Published")
            .OrderBy(item => item.Name)
            .Select(item => new ClassCatalogResponse(
                item.Id,
                item.CenterId,
                item.SportId,
                item.RoomId,
                item.Name,
                item.Description,
                item.Level,
                item.Capacity,
                item.DurationMinutes))
            .ToListAsync(cancellationToken);
    }
}
