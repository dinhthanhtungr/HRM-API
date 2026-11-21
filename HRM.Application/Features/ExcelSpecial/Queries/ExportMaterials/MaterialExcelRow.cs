namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;

public sealed record MaterialExcelRow(string? MaterialCode, string? MaterialName,
    string? CategoryCode, string? CategoryName, string? VietausCode = null);
