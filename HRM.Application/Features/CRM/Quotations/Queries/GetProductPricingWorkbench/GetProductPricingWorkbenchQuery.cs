using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbench;

public sealed class GetProductPricingWorkbenchQuery
    : PaginationQuery,
        IRequest<OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>>
{
    public string? Currency { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingWorkbenchView View { get; init; } =
        ProductPricingWorkbenchView.All;

    [JsonIgnore]
    public string NormalizedCurrency => Currency?.Trim().ToUpperInvariant() ?? string.Empty;
}
