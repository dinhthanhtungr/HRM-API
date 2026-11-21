using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleWorkChanges;

public sealed class GetSampleWorkChangesQuery : IRequest<IReadOnlyList<SampleWorkChangeDto>>
{
    [Required, MinLength(1), MaxLength(100)]
    public Guid[] SampleRequestIds { get; init; } = [];
    public SampleRequestWorkAudience Audience { get; init; }
    public SampleLabTeamFilter? LabTeam { get; init; }
}

public sealed record SampleWorkChangeDto(Guid SampleRequestId, string Version, int UnreadMessageCount);
