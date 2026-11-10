using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMemberEnrollmentOptionsService
{
    Task<MemberEnrollmentOptionsDto> GetOptionsAsync(long userId, long centerId, CancellationToken ct = default);
}
