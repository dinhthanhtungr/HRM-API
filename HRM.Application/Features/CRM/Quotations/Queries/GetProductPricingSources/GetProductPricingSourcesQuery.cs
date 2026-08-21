using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingSources;

public sealed record GetProductPricingSourcesQuery(Guid ProductId, string Currency)
    : IRequest<OperationResult<IReadOnlyList<ProductPricingSourceOptionDto>>>;
