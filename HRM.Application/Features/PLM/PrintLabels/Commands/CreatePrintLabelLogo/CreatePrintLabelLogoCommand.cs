using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.PrintLabels.Commands.CreatePrintLabelLogo;

public sealed class CreatePrintLabelLogoCommand : IRequest<OperationResult<Guid>>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid AttachmentCollectionId { get; init; }
}
