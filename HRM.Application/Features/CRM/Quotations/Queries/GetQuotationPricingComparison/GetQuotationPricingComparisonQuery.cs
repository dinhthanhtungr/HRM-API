using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingComparison;

public sealed record GetQuotationPricingComparisonQuery(Guid QuotationId)
    : IRequest<OperationResult<QuotationPricingComparisonDto>>;
