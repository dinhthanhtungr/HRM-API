using MediatR;
namespace HRM.Application.Features.Development.SampleWorkflowSimulation;
public sealed record GetSimulationScenariosQuery : IRequest<IReadOnlyList<SimulationScenario>?>;
