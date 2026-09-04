using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.WithdrawQuotationPricing;

public sealed record WithdrawQuotationPricingRequestCommand(
    Guid QuotationId,
    WithdrawQuotationPricingRequest Request)
    : IRequest<OperationResult<QuotationStatusTransitionDto>>;
