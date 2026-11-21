using HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;

namespace HRM.Application.Abstractions.Documents;

public interface IManufacturingVaExcelRenderer
{
    byte[] Render(IReadOnlyList<ManufacturingVaExcelRow> rows, CancellationToken ct = default);
}
