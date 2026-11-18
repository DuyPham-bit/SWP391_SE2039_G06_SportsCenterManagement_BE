namespace SportsCenterManagement.BLL.DTOs.Checkins;

public sealed record CheckinRequest
{
    public long? MemberId { get; init; }
    public string? Identifier { get; init; }
}

