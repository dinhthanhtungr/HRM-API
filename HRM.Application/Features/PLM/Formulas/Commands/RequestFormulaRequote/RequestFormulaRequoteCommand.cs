using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.RequestFormulaRequote;

public sealed record RequestFormulaRequoteCommand(
    Guid FormulaId,
    RequestFormulaRequoteRequest Request)
    : IRequest<OperationResult<SendInternalMessageResultDto>>;

public sealed class RequestFormulaRequoteRequest
{
    public Guid SampleRequestId { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsUrgent { get; init; }
}
