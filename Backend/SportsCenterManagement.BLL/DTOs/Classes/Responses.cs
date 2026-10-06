namespace SportsCenterManagement.BLL.DTOs.Classes;

public static class Responses
{
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
}
