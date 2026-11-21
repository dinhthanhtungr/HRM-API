using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.GetProcessingOptions;
public sealed record ProcessingStepOption(int Value, string Code);
public sealed record ProcessingOrderOptions(IReadOnlyList<string> Statuses, IReadOnlyList<ProcessingStepOption> Steps);
public sealed record GetProcessingOptionsQuery : IRequest<ProcessingOrderOptions?>;
