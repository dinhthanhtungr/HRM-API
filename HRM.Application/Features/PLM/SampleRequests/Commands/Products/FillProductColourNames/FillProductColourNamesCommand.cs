using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.FillProductColourNames;

public sealed class FillProductColourNamesCommand : IRequest<OperationResult<FillProductColourNamesResult>>
{
    public bool DryRun { get; init; } = true;
    public bool ExportPreview { get; init; }
}

public sealed record ProductColourNameCandidate(Guid ProductId, string? Name, string? ColourName, string? ColourCode);
public sealed record FillProductColourNamesResult(bool DryRun, int Examined, int Matched, int Updated,
    int ConcurrentlySkipped, IReadOnlyList<ProductColourNameCandidate> Items, bool HasMoreItems);
