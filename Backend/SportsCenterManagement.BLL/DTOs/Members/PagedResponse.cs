namespace SportsCenterManagement.BLL.DTOs.Members;

/// <summary>
/// Cấu trúc phản hồi dữ liệu phân trang chuẩn.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của các phần tử trong danh sách.</typeparam>
/// <param name="Items">Danh sách các phần tử thuộc trang hiện tại.</param>
/// <param name="Page">Chỉ số trang hiện tại (bắt đầu từ 1).</param>
/// <param name="PageSize">Số lượng phần tử tối đa trên mỗi trang.</param>
/// <param name="TotalCount">Tổng số lượng phần tử tìm thấy trên toàn hệ thống.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
