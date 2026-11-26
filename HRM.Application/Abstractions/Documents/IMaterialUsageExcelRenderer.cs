using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterialUsage;
namespace HRM.Application.Abstractions.Documents;
public interface IMaterialUsageExcelRenderer
{
    byte[] Render(IReadOnlyList<MaterialUsageExcelRow> rows);
}