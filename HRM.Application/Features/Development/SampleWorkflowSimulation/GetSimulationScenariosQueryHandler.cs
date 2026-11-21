using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using MediatR;
namespace HRM.Application.Features.Development.SampleWorkflowSimulation;
internal sealed class GetSimulationScenariosQueryHandler(ICurrentUser user) : IRequestHandler<GetSimulationScenariosQuery, IReadOnlyList<SimulationScenario>?>
{
    public Task<IReadOnlyList<SimulationScenario>?> Handle(GetSimulationScenariosQuery request, CancellationToken ct)
        => Task.FromResult(user.IsAuthenticated && user.CompanyId is not null && user.IsInRole(ApplicationRoles.Developer) ? SimulationCatalog.All() : null);
}
