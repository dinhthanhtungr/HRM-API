using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.WithdrawSendConfirmation;

public sealed record WithdrawSendConfirmationCommand(Guid QuotationId, WithdrawQuotationPricingRequest Request)
    : IRequest<OperationResult<QuotationTotalsDto>>;
