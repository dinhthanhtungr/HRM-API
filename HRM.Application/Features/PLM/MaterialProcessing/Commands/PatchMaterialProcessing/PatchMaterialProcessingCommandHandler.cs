using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Commands.PatchMaterialProcessing;
internal sealed class PatchMaterialProcessingCommandHandler(MaterialProcessingWriter writer) : IRequestHandler<PatchMaterialProcessingCommand, MaterialProcessingResult>
{
    public Task<MaterialProcessingResult> Handle(PatchMaterialProcessingCommand command, CancellationToken ct)
        => writer.SaveAsync(command.Id, null, ct, command.Request);
}
