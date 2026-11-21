using HRM.Application.Features.Warehouse.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IStockAvailableExcelRenderer
{
    byte[] Render(IReadOnlyList<StockAvailableDto> items, StockAvailableExportVisibility visibility, CancellationToken cancellationToken);
}

public sealed record StockAvailableExportVisibility(bool InactiveShelfFields);
