namespace SportsCenterManagement.BLL.DTOs.Members;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
