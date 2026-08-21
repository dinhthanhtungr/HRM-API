using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbenchDetail;

public sealed record GetProductPricingWorkbenchDetailQuery(
    Guid ProductId,
    string? Currency,
    ProductPricingSourceType? SourceType,
    Guid? SourceId)
    : IRequest<OperationResult<ProductPricingWorkbenchDetailDto>>
{
    [JsonIgnore]
    public string NormalizedCurrency => string.IsNullOrWhiteSpace(Currency)
        ? "VND"
        : Currency.Trim().ToUpperInvariant();
}
