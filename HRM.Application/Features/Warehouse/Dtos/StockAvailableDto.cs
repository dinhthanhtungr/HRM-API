using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Dtos;

public sealed class StockAvailableDto
{
    public int ShelfStockId { get; init; }

    public string Code { get; init; } = string.Empty;

    public StockType StockType { get; init; }

    public string CodeName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public decimal TotalOnHandKg { get; init; }

    public decimal PhysicalOnHandKg { get; init; }

    public decimal InactiveShelfOnHandKg { get; init; }

    public decimal ReservedOpenAllKg { get; set; }

    public decimal AvailableKg { get; set; }

    public IReadOnlyList<ReservedVaCodeDto> ReservedVaCodes { get; set; } = Array.Empty<ReservedVaCodeDto>();

    public List<StockAvailableDetailDto> StockDetailAvailables { get; init; } = new();
}

public sealed class StockAvailableDetailDto
{
    public IReadOnlyList<DefectReasonDto> DefectReasons { get; set; } = [];
    public string? LotNo { get; init; }

    public string ShelfStockCode { get; init; } = string.Empty;

    public string CompanyName { get; init; } = string.Empty;

    public decimal OnHandKg { get; init; }

    public bool ShelfIsActive { get; init; }

    // Shelf eligibility only; each mutation must still validate its own stock type and capability.
    public bool CanUse { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed class ReservedVaCodeDto
{
    public Guid? MfgProductionOrderId { get; init; }
    public string VaCode { get; init; } = string.Empty;

    public decimal ReservedKg { get; init; }

    public DateTime CreatedDate { get; init; }
}
