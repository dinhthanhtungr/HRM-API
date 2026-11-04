namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public class QCInputByQCExcelExportData
{
    public List<QCInputByQCExportRow> DetailRows { get; set; } = new();
    public List<GetSummaryQCInput> ReportRows { get; set; } = new();
}

public class QCInputCategoryReportRow
{
    public string CategoryName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal QtyKg { get; set; }
    public decimal Percent { get; set; }
}

public class QCInputQcStatusReportRow
{
    public string StatusName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal QtyKg { get; set; }
    public decimal Percent { get; set; }
}
