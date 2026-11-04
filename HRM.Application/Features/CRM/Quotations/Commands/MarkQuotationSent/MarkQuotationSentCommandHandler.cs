using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.MarkQuotationSent;

internal sealed class MarkQuotationSentCommandHandler(QuotationSendService service)
    : IRequestHandler<MarkQuotationSentCommand, OperationResult>
{
    public Task<OperationResult> Handle(MarkQuotationSentCommand command, CancellationToken cancellationToken)
        => service.ExecuteAsync(command.QuotationId, command.Request, null, cancellationToken);
}
