using MediatR;
namespace HRM.Application.Features.Development.SampleWorkflowSimulation;
public sealed record EvaluateSimulationCommand(SimulationStepRequest Request) : IRequest<SimulationView?>;
