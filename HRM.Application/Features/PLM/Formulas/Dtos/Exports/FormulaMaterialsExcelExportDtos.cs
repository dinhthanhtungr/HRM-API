namespace HRM.Application.Features.PLM.Formulas.Dtos.Exports;

public sealed class FormulaMaterialsExcelExportFileDto
{
    public string FileName { get; set; } = "danh-sach-nvl-cong-thuc.xlsx";
    public string ContentType { get; set; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public byte[] Content { get; set; } = [];
}

public sealed class FormulaMaterialsExcelExportDocumentDto
{
    public string FormulaExternalId { get; set; } = string.Empty;
    public string? FormulaName { get; set; }
    public IReadOnlyList<FormulaMaterialsExcelExportLineDto> Materials { get; set; } = [];
}

public sealed class FormulaMaterialsExcelExportLineDto
{
    public int LineNo { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? LatestUnitPrice { get; set; }
}
