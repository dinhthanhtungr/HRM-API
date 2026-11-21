using HRM.Application.Features.Reports.PLPU;

namespace HRM.Application.Features.Reports.CompanyOverview.ExportCompanyOverview;

public sealed record CompanySalesExportRow(DateTime Date, string OrderCode, string Customer,
    string ProductCode, string ProductName, decimal QuantityKg, decimal AmountVnd);

public sealed record CompanyOverviewExportData(CompanyOverviewDto Report,
    IReadOnlyList<CompanySalesExportRow> Sales,
    IReadOnlyList<PLPUPurchaseOrderDetailReportDto> RawMaterials,
    IReadOnlyList<CompanyPurchaseFact> Supplies);

public interface ICompanyOverviewExcelRenderer
{
    byte[] Render(CompanyOverviewExportData data);
}

public sealed record CompanyOverviewExportFile(byte[] Content, string FileName);
