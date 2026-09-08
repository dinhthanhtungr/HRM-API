namespace HRM.Application.Features.PLM.Boms.Dtos;

/// <summary>PATCH chỉ sửa metadata của Draft; item chỉ được thay thế bằng PUT.</summary>
public sealed class PatchBomVersionRequest
{
    public decimal? BaseOutputQuantity { get; init; }
    public string? OutputUnit { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<string> ClearFields { get; init; } = [];
}
