namespace HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;

public sealed record ManufacturingVaExcelItem(string? Code, string? Name, decimal StandardQuantity);

/// <summary>Mỗi bản ghi là một VA; khối lượng nhập chỉ cộng một lần, trước khi trải chi tiết NVL.</summary>
public sealed record ManufacturingVaExcelRow(
    string VaCode, DateTime CreatedDate, DateTime? LastReceiptDate, string OrderCodes,
    string CustomerCodes, string MfgCode, string Case, string ProductionType, string Output,
    string Status, string ProductExternalId, string? ColourCode, string? ProductName,
    decimal? PlannedQuantity, decimal? ReceivedQuantity, bool IsSingleMaterial,
    string Operation, string? CategoryName, string Notes, IReadOnlyList<ManufacturingVaExcelItem> Items);
