using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.InternalMail.Queries.GetSampleRequestSummary;
internal sealed class GetSampleRequestConversationSummaryQueryHandler(ICurrentUser user, InternalMailAreaAccessService areas,
    ISampleRequestConversationSummaryStore store) : IRequestHandler<GetSampleRequestConversationSummaryQuery, ConversationSummaryDto?>
{
    public async Task<ConversationSummaryDto?> Handle(GetSampleRequestConversationSummaryQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty || user.EmployeeId is null || user.EmployeeId == Guid.Empty) return null;
        var id = await areas.Conversations().AsNoTracking().Where(c => c.InternalConversationId == request.ConversationId &&
            c.CompanyId == company && c.RelatedType == InternalMailRelatedType.SampleRequest).Select(c => c.RelatedId).FirstOrDefaultAsync(ct);
        return id.HasValue ? await store.ReadAsync(company, id.Value, ct) : null;
    }
}
