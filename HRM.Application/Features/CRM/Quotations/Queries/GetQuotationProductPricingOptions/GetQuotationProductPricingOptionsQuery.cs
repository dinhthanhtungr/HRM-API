using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions;

public sealed class GetQuotationProductPricingOptionsQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<QuotationProductPricingOptionDto>>>
{
    public string? RequestType { get; init; }
    public SampleRequestStatus? Status { get; set; }
    public Guid? QuotationId { get; init; }
    public QuotationStatus? QuotationStatus { get; init; }
    public string? Currency { get; init; }

    [JsonIgnore]
    public string? NormalizedRequestType => string.IsNullOrWhiteSpace(RequestType)
        ? null
        : RequestType.Trim();

    [JsonIgnore]
    public string NormalizedCurrency => Currency?.Trim().ToUpperInvariant() ?? string.Empty;

}
