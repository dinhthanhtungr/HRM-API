using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleTrialFeedbackRevisions;

public sealed record GetSampleTrialFeedbackRevisionsQuery(Guid SampleRequestId, Guid TrialId, int PageNumber = 1)
    : IRequest<SampleTrialFeedbackRevisionsDto?>;
