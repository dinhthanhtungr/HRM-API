using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestFormulaChangeRequest;

public sealed class CreateSampleRequestFormulaChangeRequestCommand
    : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public Guid SampleRequestId { get; set; }
    public Guid RequestedFormulaId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsUrgent { get; set; }
    public IReadOnlyList<Guid> RecipientEmployeeIds { get; set; } = Array.Empty<Guid>();
}
