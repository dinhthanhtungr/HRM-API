using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

/// <summary>Only the two queue read queries may execute inside a simulated identity scope.</summary>
public sealed class SampleWorkPreviewReadOnlyBehavior<TRequest, TResponse>(SampleWorkPreviewSession session)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (session.User != null && request is not GetSampleRequestDailyWorkQuery && request is not GetSampleRequestSampleTrialsQuery)
            throw new UnauthorizedAccessException("Sample work preview is read-only.");
        return next();
    }
}
