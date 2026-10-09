namespace SportsCenterManagement.BLL.DTOs.Checkins;

public sealed record CheckinRequest
{
    public long? MemberId { get; init; }
    public string? Identifier { get; init; }
}

public sealed record CheckinResponse(
    long Id,
    long MemberId,
    string MemberName,
    string MemberCode,
    string PackageName,
    string CheckInTime,
    string ReceptionistName
);
