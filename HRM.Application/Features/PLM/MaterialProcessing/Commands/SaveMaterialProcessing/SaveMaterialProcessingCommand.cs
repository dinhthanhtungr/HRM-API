using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Commands.SaveMaterialProcessing;
public sealed record SaveMaterialProcessingCommand(Guid? Id, SaveMaterialProcessingRequest Request) : IRequest<MaterialProcessingResult>
{
    public Action<string, double>? RecordTiming { get; init; }
}
