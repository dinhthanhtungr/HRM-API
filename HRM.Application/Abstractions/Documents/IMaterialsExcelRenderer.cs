using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;

namespace HRM.Application.Abstractions.Documents;

public interface IMaterialsExcelRenderer
{
    byte[] Render(IReadOnlyList<MaterialExcelRow> rows);
}
