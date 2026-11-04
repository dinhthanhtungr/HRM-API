using HRM.Application.Features.DevAndQA.IQC.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IIqcExcelRenderer
{
    byte[] ExportExcel(QCInputByQCExcelExportData data);
}
