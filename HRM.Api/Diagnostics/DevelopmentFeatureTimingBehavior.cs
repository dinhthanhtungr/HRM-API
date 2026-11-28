using HRM.Application.Commons.Diagnostics;
using MediatR;

namespace HRM.Api.Diagnostics;

internal sealed class DevelopmentFeatureTimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly string Feature = "feature_" + typeof(TRequest).Name.Split('`')[0];

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var start = RequestTiming.StartStep();
        try { return await next(); }
        finally { RequestTiming.FinishStep(Feature, ref start); }
    }
}
