using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.WithdrawSendConfirmation;

internal sealed class WithdrawSendConfirmationCommandHandler(QuotationSendService service)
    : IRequestHandler<WithdrawSendConfirmationCommand, OperationResult<QuotationTotalsDto>>
{
    public Task<OperationResult<QuotationTotalsDto>> Handle(WithdrawSendConfirmationCommand request, CancellationToken cancellationToken)
        => service.WithdrawConfirmationAsync(request.QuotationId, request.Request, cancellationToken);
}
