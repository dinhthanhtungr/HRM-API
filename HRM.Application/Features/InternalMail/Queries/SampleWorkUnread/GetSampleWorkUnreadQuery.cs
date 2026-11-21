using HRM.Application.Features.InternalMail.Services;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.SampleWorkUnread;

internal sealed record GetSampleWorkUnreadQuery(Guid[] SampleRequestIds) : IRequest<IReadOnlyDictionary<Guid, int>>;

internal sealed class GetSampleWorkUnreadQueryHandler(InternalMailAreaAccessService areas)
    : IRequestHandler<GetSampleWorkUnreadQuery, IReadOnlyDictionary<Guid, int>>
{
    public Task<IReadOnlyDictionary<Guid, int>> Handle(GetSampleWorkUnreadQuery request, CancellationToken ct)
        => areas.SampleWorkUnreadAsync(request.SampleRequestIds, ct);
}
