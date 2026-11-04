using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.ConfirmQuotationSent;

internal sealed class ConfirmQuotationSentCommandHandler(QuotationSendService service)
    : IRequestHandler<ConfirmQuotationSentCommand, OperationResult>
{
    public Task<OperationResult> Handle(ConfirmQuotationSentCommand command, CancellationToken cancellationToken)
        => service.ExecuteAsync(command.QuotationId,
            new MarkQuotationSentRequest { ExpectedUpdatedDate = command.Request.ExpectedUpdatedDate },
            command.Request.RequestMessageId, cancellationToken, confirmationOnly: true);
}
