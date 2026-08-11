namespace HRM.Application.Features.PLM.Materials.Dtos;

public sealed class UpdateMaterialSupplierPriceRequest
{
    public decimal NewPrice { get; init; }
    public decimal? ExpectedCurrentPrice { get; init; }
    public string? Currency { get; init; }
}

public sealed class UpdateMaterialSupplierPriceResultDto
{
    public Guid MaterialsSupplierId { get; init; }

    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;

    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;

    public decimal? OldPrice { get; init; }
    public decimal NewPrice { get; init; }
    public string? Currency { get; init; }

    public Guid PriceHistoryId { get; init; }
    public DateTime UpdatedDate { get; init; }
    public Guid UpdatedByEmployeeId { get; init; }
}
