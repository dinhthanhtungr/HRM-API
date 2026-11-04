using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RequestSampleRequestPriceQuote;

public sealed record RequestSampleRequestPriceQuoteCommand(
    Guid SampleRequestId,
    RequestSampleRequestPriceQuoteRequest Request)
    : IRequest<OperationResult<SendInternalMessageResultDto>>;

public sealed class RequestSampleRequestPriceQuoteRequest
{
    public Guid? FormulaId { get; init; }
    public string? Message { get; init; }
    public bool IsUrgent { get; init; }
}
