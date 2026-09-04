using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.RequestSampleRequestPriceQuote;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.RequestFormulaRequote;

/// <summary>
/// Sends a re-quote request for a Formula into its Sample Request conversation.
/// </summary>
internal sealed class RequestFormulaRequoteCommandHandler
    : IRequestHandler<RequestFormulaRequoteCommand, OperationResult<SendInternalMessageResultDto>>
{
    private readonly ISender _sender;

    public RequestFormulaRequoteCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        RequestFormulaRequoteCommand command,
        CancellationToken cancellationToken)
    {
        return await _sender.Send(
            new RequestSampleRequestPriceQuoteCommand(
                command.Request.SampleRequestId,
                new RequestSampleRequestPriceQuoteRequest
                {
                    FormulaId = command.FormulaId,
                    Message = command.Request.Message,
                    IsUrgent = command.Request.IsUrgent
                }),
            cancellationToken);
    }
}
