using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Commands.SaveMaterialProcessing;
internal sealed class SaveMaterialProcessingCommandHandler(MaterialProcessingWriter writer) : IRequestHandler<SaveMaterialProcessingCommand, MaterialProcessingResult>
{
    public Task<MaterialProcessingResult> Handle(SaveMaterialProcessingCommand command, CancellationToken ct)
        => writer.SaveAsync(command.Id, command.Request, ct);
}
