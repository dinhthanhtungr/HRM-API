using HRM.Application.Features.ExcelSpecial.Queries.ExportMerchandiseOrderVa;

namespace HRM.Application.Abstractions.Documents;

public interface IMerchandiseOrderVaExcelRenderer
{
    byte[] Render(IReadOnlyList<MerchandiseOrderVaExcelRow> rows);
}
