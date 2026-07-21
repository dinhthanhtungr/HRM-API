using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleRequestColourCode;

public sealed class UpdateSampleRequestColourCodeCommand : IRequest<OperationResult<string>>
{
    public Guid SampleRequestId { get; set; }
    public string? ColourCode { get; set; }
}
