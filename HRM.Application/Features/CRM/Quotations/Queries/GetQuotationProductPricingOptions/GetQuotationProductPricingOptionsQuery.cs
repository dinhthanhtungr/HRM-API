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
    public const string DefaultRequestType = "New";

    public string? RequestType { get; init; } = DefaultRequestType;
    public SampleRequestStatus? Status { get; set; }
    public Guid? QuotationId { get; init; }
    public QuotationStatus? QuotationStatus { get; init; }

    [JsonIgnore]
    public string NormalizedRequestType => string.IsNullOrWhiteSpace(RequestType)
        ? DefaultRequestType
        : RequestType.Trim();

}
