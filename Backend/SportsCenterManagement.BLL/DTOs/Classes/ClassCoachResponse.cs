namespace SportsCenterManagement.BLL.DTOs.Classes;

/// <summary>
/// Thông tin Huấn luyện viên được phân công phụ trách lớp học.
/// </summary>
/// <param name="ClassId">Mã định danh lớp học.</param>
/// <param name="CoachId">Mã định danh Huấn luyện viên.</param>
/// <param name="CoachName">Họ và tên Huấn luyện viên.</param>
/// <param name="CoachCode">Mã số hồ sơ HLV (ví dụ: "CH00001").</param>
/// <param name="IsPrimary">true nếu là Huấn luyện viên chính, false nếu là trợ giảng.</param>
/// <param name="AssignedDate">Ngày phân công vào lớp.</param>
/// <param name="Specialization">Chuyên môn thể thao giảng dạy.</param>
public sealed record ClassCoachResponse(
    long ClassId,
    long CoachId,
    string CoachName,
    string CoachCode,
    bool IsPrimary,
    DateOnly? AssignedDate,
    string? Specialization);
