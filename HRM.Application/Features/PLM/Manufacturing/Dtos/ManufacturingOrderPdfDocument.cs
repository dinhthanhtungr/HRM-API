namespace HRM.Application.Features.PLM.Manufacturing.Dtos;

public sealed class ManufacturingOrderPdfDocument
{
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? RequestDate { get; set; }
    public string BagType { get; set; } = string.Empty;
    public string? CopiedFromCode { get; set; }
    public string? FormulaSelectList { get; set; }
    public DateTime CreatedDate { get; set; }
    public ManufacturingOrderPdfHeader Header { get; set; } = new();
    public List<ManufacturingOrderPdfMaterial> Materials { get; set; } = [];
}

public sealed class ManufacturingOrderPdfHeader
{
    public string? FormulaExternalId { get; set; }
    public decimal? TotalProductionQuantity { get; set; }
    public int? NumOfBatches { get; set; }
    public string? QcCheck { get; set; }
    public string? ColourCode { get; set; }
    public string? Name { get; set; }
    public string? CustomerCode { get; set; }
    public string? MerchandiseOrderExternalId { get; set; }
    public string? PONo { get; set; }
    public double? UsageRate { get; set; }
    public string? LabNote { get; set; }
    public string? Requirement { get; set; }
    public string? PlpuNote { get; set; }
}

public sealed class ManufacturingOrderPdfMaterial
{
    public int LineNo { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public decimal Quantity { get; set; }
    public string? LotNo { get; set; }
}
