using HRM.Application.Features.PLM.SampleRequests.Services;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleLabTeams;

internal sealed class GetSampleLabTeamsQueryHandler(SampleRequestLabTeamScope teams) : IRequestHandler<GetSampleLabTeamsQuery, SampleLabTeamContext>
{
    public Task<SampleLabTeamContext> Handle(GetSampleLabTeamsQuery request, CancellationToken ct) => teams.ReadAsync(ct);
}
