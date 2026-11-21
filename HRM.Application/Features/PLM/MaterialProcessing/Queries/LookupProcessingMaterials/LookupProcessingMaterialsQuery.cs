using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.LookupProcessingMaterials;
public sealed record LookupProcessingMaterialsQuery(string? Keyword, string? ProcessingType = null) : IRequest<IReadOnlyList<ProcessingMaterialOption>?>
{
    public string? ServerTiming { get; internal set; }
}
