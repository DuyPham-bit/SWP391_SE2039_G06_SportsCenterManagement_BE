namespace SportsCenterManagement.BLL.DTOs.Catalog;

public sealed record PublicCenterDto(
    long Id,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    string? Description,
    TimeOnly? OpeningTime,
    TimeOnly? ClosingTime);

public sealed record PublicSportDto(
    long Id,
    string Name,
    string? Description);

public sealed record PublicRoomDto(
    long Id,
    string Name,
    string? RoomType,
    int Capacity,
    string? LocationDescription);

public sealed record PublicClassDto(
    long Id,
    string Name,
    long SportId,
    long? RoomId,
    string? Level);

public sealed record PublicCatalogResponse(
    PublicCenterDto Center,
    IReadOnlyList<PublicSportDto> Sports,
    IReadOnlyList<PublicRoomDto> Rooms,
    IReadOnlyList<PublicClassDto> Classes);
