namespace HRM.Application.Features.CRM.Quotations.Queries.Tracking;

public sealed record QuotationTrackingDto(
    Guid QuotationId, string ExternalId, string CustomerName, string Status, DateTime QuotationDate);

public sealed record QuotationTrackingLineDto(string ProductName, decimal Quantity);

public sealed record QuotationTrackingDetailDto(
    QuotationTrackingDto Quotation, IReadOnlyList<QuotationTrackingLineDto> Lines);
