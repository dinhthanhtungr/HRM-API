using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Commands.ConfirmMaterialProcessing;
internal sealed class ConfirmMaterialProcessingCommandHandler(MaterialProcessingWriter writer) : IRequestHandler<ConfirmMaterialProcessingCommand, MaterialProcessingResult>
{
    public Task<MaterialProcessingResult> Handle(ConfirmMaterialProcessingCommand command, CancellationToken ct)
        => writer.ConfirmAsync(command.Id, command.Request, ct);
}
