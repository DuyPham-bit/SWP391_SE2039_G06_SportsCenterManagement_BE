namespace SportsCenterManagement.BLL.DTOs.Classes;

public sealed record ClassOptionResponse(long Id, string Name, string? Type, int Capacity);
public sealed record SportOptionResponse(long Id, string Name, string? Description);
