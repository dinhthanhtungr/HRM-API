using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.PrintLabels.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.PrintLabels.Commands.CreatePrintLabelCatalog;

public sealed class CreatePrintLabelLogoCommand : IRequest<OperationResult<Guid>>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid AttachmentCollectionId { get; init; }
}

public sealed class CreatePrintLabelTemplateCommand : IRequest<OperationResult<Guid>>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? LabelType { get; init; }
    public string? Instructions { get; init; }
    public decimal WidthMm { get; init; }
    public decimal HeightMm { get; init; }
    public Guid? AttachmentCollectionId { get; init; }
    public IReadOnlyList<Guid> PrintLabelLogoIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PrintLabelElementRequest> Elements { get; init; } = Array.Empty<PrintLabelElementRequest>();
}
