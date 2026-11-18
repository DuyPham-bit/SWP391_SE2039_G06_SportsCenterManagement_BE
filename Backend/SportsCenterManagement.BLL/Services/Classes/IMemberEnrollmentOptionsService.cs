using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Services.Classes;

public interface IMemberEnrollmentOptionsService
{
    Task<MemberEnrollmentOptionsDto> GetOptionsAsync(long userId, long centerId, CancellationToken ct = default);
}
