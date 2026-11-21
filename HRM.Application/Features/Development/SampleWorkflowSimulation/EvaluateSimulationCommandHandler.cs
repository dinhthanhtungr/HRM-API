using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using MediatR;
namespace HRM.Application.Features.Development.SampleWorkflowSimulation;
internal sealed class EvaluateSimulationCommandHandler(ICurrentUser user) : IRequestHandler<EvaluateSimulationCommand, SimulationView?>
{
    public Task<SimulationView?> Handle(EvaluateSimulationCommand request, CancellationToken ct)
        => Task.FromResult(user.IsAuthenticated && user.CompanyId is not null && user.IsInRole(ApplicationRoles.Developer)
            ? SimulationTransitions.Evaluate(request.Request) : null);
}
