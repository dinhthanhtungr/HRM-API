using HRM.Application.Features.Reports.PLPU;
namespace HRM.Application.Abstractions.Documents;
public interface IPLPUFinishReportExcelRenderer
{
    byte[] ExportFinishReportExcel(List<FinishRow> rows);
}
