using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricing;

public sealed record GetQuotationProductPricingQuery(Guid ProductId)
    : IRequest<OperationResult<QuotationResolvedProductPricingDto>>;
