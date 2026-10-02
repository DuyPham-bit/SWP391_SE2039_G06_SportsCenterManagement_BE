using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ quản lý danh mục lớp học và phân công Huấn luyện viên (UC-13).
/// </summary>
public interface IClassService
{
    /// <summary>
    /// Lấy danh sách các lớp học đã mở và công bố tại trung tâm thể thao.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Danh sách các lớp học.</returns>
    Task<IReadOnlyList<ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phân công Huấn luyện viên vào lớp học.
    /// </summary>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="request">Thông tin HLV và vai trò.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin kết quả phân công.</returns>
    Task<ClassCoachResponse> AssignCoachToClassAsync(
        long classId,
        AssignCoachRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phân công Huấn luyện viên vào lớp học theo phạm vi trung tâm cụ thể.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="request">Thông tin HLV và vai trò.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin kết quả phân công.</returns>
    Task<ClassCoachResponse> AssignCoachToClassAsync(
        long centerId,
        long classId,
        AssignCoachRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách Huấn luyện viên phụ trách lớp học.
    /// </summary>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Danh sách HLV phân công.</returns>
    Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(
        long classId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách Huấn luyện viên phụ trách lớp học theo phạm vi trung tâm cụ thể.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Danh sách HLV phân công.</returns>
    Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(
        long centerId,
        long classId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy phân công Huấn luyện viên khỏi lớp học.
    /// </summary>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="coachId">Mã định danh Huấn luyện viên.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    Task UnassignCoachFromClassAsync(
        long classId,
        long coachId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy phân công Huấn luyện viên khỏi lớp học theo phạm vi trung tâm cụ thể.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="coachId">Mã định danh Huấn luyện viên.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    Task UnassignCoachFromClassAsync(
        long centerId,
        long classId,
        long coachId,
        CancellationToken cancellationToken = default);
}
