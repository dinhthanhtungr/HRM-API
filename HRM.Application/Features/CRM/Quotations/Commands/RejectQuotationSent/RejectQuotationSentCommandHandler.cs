using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.RejectQuotationSent;

internal sealed class RejectQuotationSentCommandHandler(QuotationSendService service) : IRequestHandler<RejectQuotationSentCommand, OperationResult>
{
    public Task<OperationResult> Handle(RejectQuotationSentCommand command, CancellationToken cancellationToken)
        => service.RejectConfirmationAsync(command.QuotationId, command.Request, cancellationToken);
}
