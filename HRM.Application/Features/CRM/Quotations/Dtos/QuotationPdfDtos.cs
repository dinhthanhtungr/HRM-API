using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationPdfFileDto
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/pdf";
    public byte[] Content { get; init; } = [];
}

public sealed class QuotationPdfDocumentDto
{
    public string ExternalId { get; init; } = string.Empty;
    public DateTime QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string Currency { get; init; } = string.Empty;

    public string CompanyName { get; init; } = string.Empty;
    public string? CompanyAddress { get; init; }
    public string? CompanyPhone { get; init; }
    public string? CompanyEmail { get; init; }

    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerAddress { get; init; }
    public string? CustomerPhone { get; init; }
    public string? CustomerFax { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }

    public string SaleEmployeeName { get; init; } = string.Empty;
    public string? SaleEmployeePhone { get; init; }
    public string? SaleEmployeeEmail { get; init; }

    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }

    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? Note { get; init; }

    public IReadOnlyList<QuotationPdfLineDto> Lines { get; init; } = [];
}

public sealed class QuotationPdfLineDto
{
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public QuotationLinePriceMode PriceMode { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal LineTotal { get; init; }
    public string? Note { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyList<QuotationPdfPriceTierDto> PriceTiers { get; init; } = [];
}

public sealed class QuotationPdfPriceTierDto
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int SortOrder { get; init; }
}
