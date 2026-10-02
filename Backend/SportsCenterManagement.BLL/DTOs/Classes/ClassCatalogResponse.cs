namespace SportsCenterManagement.BLL.DTOs.Classes;

/// <summary>
/// Thông tin chi tiết một lớp học được công bố trong danh mục của trung tâm thể thao.
/// </summary>
/// <param name="Id">Mã định danh lớp học.</param>
/// <param name="CenterId">Mã trung tâm thể thao tổ chức lớp.</param>
/// <param name="SportId">Mã bộ môn thể thao.</param>
/// <param name="RoomId">Mã phòng tập/sân tập (nếu có).</param>
/// <param name="Name">Tên lớp học.</param>
/// <param name="Description">Mô tả tóm tắt nội dung lớp học.</param>
/// <param name="Level">Trình độ của lớp học (Beginner, Intermediate, Advanced,...).</param>
/// <param name="Capacity">Sĩ số học viên tối đa của lớp.</param>
/// <param name="DurationMinutes">Thời lượng mỗi buổi học tính bằng phút.</param>
public sealed record ClassCatalogResponse(
    long Id,
    long CenterId,
    long SportId,
    long? RoomId,
    string Name,
    string? Description,
    string? Level,
    int Capacity,
    int DurationMinutes);
