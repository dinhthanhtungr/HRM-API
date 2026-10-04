namespace HRM.Application.Features.MRO.Equipment.Services;

public sealed record EquipmentResult<T>(T? Data, int StatusCode = 200, string? ErrorCode = null)
{
    public bool Success => StatusCode < 400;
    public static EquipmentResult<T> Fail(int status, string code) => new(default, status, code);
}
