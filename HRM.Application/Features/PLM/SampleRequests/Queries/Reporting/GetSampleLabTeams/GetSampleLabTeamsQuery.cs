using HRM.Application.Features.PLM.SampleRequests.Services;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleLabTeams;

public sealed record GetSampleLabTeamsQuery : IRequest<SampleLabTeamContext>;
