namespace SportsCenterManagement.BLL.DTOs.Classes;

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
