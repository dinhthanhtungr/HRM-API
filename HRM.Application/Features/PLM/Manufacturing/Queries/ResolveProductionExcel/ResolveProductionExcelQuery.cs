using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ResolveProductionExcel;

public sealed record ProductionExcelRow(string ColourCode, string Name, string LotNo, string Quality);
public sealed record ProductionExcelItem(Guid ItemId, int ItemType, Guid CategoryId, string Code,
    string Name, string StockCode, string Unit, string LotNo, int StockType, decimal StockKg, decimal Percent);
public sealed record ProductionExcelResolution(int Row, ProductionExcelItem? Item, string? Error);
public sealed record ResolveProductionExcelQuery(Guid Id, ProductionExcelRow[] Rows)
    : IRequest<OperationResult<IReadOnlyList<ProductionExcelResolution>>>;
public sealed record ProductionExcelRequest(ProductionExcelRow[] Rows);
