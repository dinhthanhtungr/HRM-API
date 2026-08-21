using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingVersions;

public sealed record GetProductPricingVersionsQuery(Guid ProductId, string? Currency)
    : IRequest<OperationResult<IReadOnlyList<ProductPricingVersionDto>>>;
