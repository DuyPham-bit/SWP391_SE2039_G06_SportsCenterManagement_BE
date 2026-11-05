namespace SportsCenterManagement.BLL.DTOs.Media;

public sealed record MediaStorageStatusDto(bool IsConnected, int TotalMedia, string Provider)
{
    public int TotalImages => TotalMedia;
}
