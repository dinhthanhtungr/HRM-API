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

    public decimal ReservedOpenAllKg { get; set; }

    public decimal AvailableKg { get; set; }

    public IReadOnlyList<ReservedVaCodeDto> ReservedVaCodes { get; set; } = Array.Empty<ReservedVaCodeDto>();

    public List<StockAvailableDetailDto> StockDetailAvailables { get; init; } = new();
}

public sealed class StockAvailableDetailDto
{
    public string? LotNo { get; init; }

    public string ShelfStockCode { get; init; } = string.Empty;

    public string CompanyName { get; init; } = string.Empty;

    public decimal OnHandKg { get; init; }
}

public sealed class ReservedVaCodeDto
{
    public string VaCode { get; init; } = string.Empty;

    public decimal ReservedKg { get; init; }

    public DateTime CreatedDate { get; init; }
}
