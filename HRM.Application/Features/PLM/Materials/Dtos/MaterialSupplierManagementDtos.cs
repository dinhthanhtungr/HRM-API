namespace HRM.Application.Features.PLM.Materials.Dtos;

public sealed class MaterialSupplierLookupDto
{
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
}

public sealed class CreateMaterialSupplierRequest
{
    public Guid MaterialId { get; init; }
    public Guid SupplierId { get; init; }
    public decimal? CurrentPrice { get; init; }
    public string? Currency { get; init; }
    public bool IsPreferred { get; init; }
    public int? MinDeliveryDays { get; init; }
}

public sealed class PatchMaterialSupplierRequest
{
    public decimal? NewPrice { get; init; }
    public decimal? ExpectedCurrentPrice { get; init; }
    public string? Currency { get; init; }
    public bool? IsPreferred { get; init; }
    public int? MinDeliveryDays { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class MaterialSupplierManagementDto
{
    public Guid MaterialsSupplierId { get; init; }
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public decimal? CurrentPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsPreferred { get; init; }
    public bool IsActive { get; init; }
    public int? MinDeliveryDays { get; init; }
    public DateTime UpdatedDate { get; init; }
    public Guid UpdatedByEmployeeId { get; init; }
    public Guid? PriceHistoryId { get; init; }
    public bool PriceChanged { get; init; }
}
