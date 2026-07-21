using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class CreateQuotationRequest
{
    public string? ExternalId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? ContactId { get; init; }
    public string? ContactName { get; init; }
    public string Currency { get; init; } = "VND";
    public decimal ExchangeRate { get; init; } = 1m;
    public DateTime? QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<QuotationLineRequest> Lines { get; init; } = [];
}

public sealed class UpdateQuotationRequest
{
    public Guid? CustomerId { get; init; }
    public Guid? ContactId { get; init; }
    public string? ContactName { get; init; }
    public string? Currency { get; init; }
    public decimal? ExchangeRate { get; init; }
    public DateTime? QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? Note { get; init; }
}

public sealed class ReplaceQuotationLinesRequest
{
    public IReadOnlyList<QuotationLineRequest> Lines { get; init; } = [];
}

public sealed class QuotationLineRequest
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal TaxPercent { get; init; }
    public string? Note { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class RefreshQuotationPricesRequest
{
    public IReadOnlyList<QuotationPriceRequest> Lines { get; init; } = [];
}

public sealed class QuotationPriceRequest
{
    public Guid QuotationLineId { get; init; }
    public decimal UnitPrice { get; init; }
}

public sealed class MarkQuotationSentRequest
{
    public string? Note { get; init; }
}

public sealed class QuotationCreateResultDto
{
    public Guid QuotationId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
}

public sealed class QuotationTotalsDto
{
    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed class QuotationListItemDto
{
    public Guid QuotationId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public QuotationStatus Status { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public DateTime? SentDate { get; init; }
    public int LineCount { get; init; }
}

public sealed class QuotationDetailDto
{
    public Guid QuotationId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid? ContactId { get; init; }
    public string? ContactName { get; init; }
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public QuotationStatus Status { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal ExchangeRate { get; init; }
    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public DateTime? SentDate { get; init; }
    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? Note { get; init; }
    public int Version { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<QuotationLineDto> Lines { get; init; } = [];
    public IReadOnlyList<QuotationStatusHistoryDto> StatusHistories { get; init; } = [];
}

public sealed class QuotationLineDto
{
    public Guid QuotationLineId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal LineTotal { get; init; }
    public string? Note { get; init; }
    public int SortOrder { get; init; }
}

public sealed class QuotationStatusHistoryDto
{
    public Guid Id { get; init; }
    public QuotationStatus FromStatus { get; init; }
    public QuotationStatus ToStatus { get; init; }
    public string? Note { get; init; }
    public Guid ChangedBy { get; init; }
    public string ChangedByName { get; init; } = string.Empty;
    public DateTime ChangedDate { get; init; }
}
