namespace SportsCenterManagement.BLL.DTOs.Checkins;

public sealed class CounterCheckinRequest
{
    public long? MemberId { get; set; }

    public string? MemberCode { get; set; }
}
