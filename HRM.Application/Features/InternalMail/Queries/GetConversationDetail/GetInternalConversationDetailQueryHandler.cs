using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationDetail;

internal sealed class GetInternalConversationDetailQueryHandler
    : IRequestHandler<GetInternalConversationDetailQuery, InternalConversationDetailDto?>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMailAreaAccessService _areas;
    private readonly InternalConversationSampleRequestInfoResolver _sampleRequestInfoResolver;
    private readonly InternalConversationQuotationInfoResolver _quotationInfoResolver;

    public GetInternalConversationDetailQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver,
        InternalMailAreaAccessService areas)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _sampleRequestInfoResolver = sampleRequestInfoResolver;
        _quotationInfoResolver = quotationInfoResolver;
    }

    public async Task<InternalConversationDetailDto?> Handle(
        GetInternalConversationDetailQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return null;
        }

        using var timing = new NotificationQueryTiming(null, "thread-detail",
            System.Diagnostics.Activity.Current?.GetTagItem(NotificationDiagnosticTags.ThreadTiming) is "enabled");
        // The authorized query both checks access and loads the result in one round trip.
        var conversation = await timing.RunAsync("thread-access", () => _areas.Conversations()
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.CompanyId == companyId.Value &&
                x.IsActive)
            // DescribeAsync below supplies the canonical unread count; do not also count legacy read states here.
            .Select(InternalConversationDetailProjection.ForEmployee(employeeId.Value, _dbContext.InternalMessages,
                _dbContext.InternalConversations, includeLegacyUnread: false))
            .FirstOrDefaultAsync(cancellationToken));

        if (conversation is null)
        {
            return null;
        }

        var group = (await timing.RunAsync("group-info", () => _areas.GroupInfoAsync([conversation.ConversationId], cancellationToken)))[conversation.ConversationId];
        if (request.AreaCode != null && request.AreaCode != InternalMailAreaAccessService.AreaOf(group.GroupType)) return null;
        var parent = group.RootConversationId != conversation.ConversationId
            ? await timing.RunAsync("parents", () => _dbContext.InternalConversations.AsNoTracking().FirstAsync(c =>
                c.InternalConversationId == group.RootConversationId && c.CompanyId == companyId.Value && c.IsActive, cancellationToken))
            : null;
        conversation.Areas = await timing.RunAsync("thread-areas", () => _areas.DescribeAsync(conversation, cancellationToken, parent, group, timing));
        conversation.AreaCode = InternalMailAreaAccessService.AreaOf(group.GroupType);
        conversation.GroupConversationId = group.RootConversationId;
        if (InternalMailAreaAccessService.IsPrivateArea(conversation.RelatedType))
        {
            conversation.CanManageParticipants = conversation.Participants.Any(p => p.EmployeeId == employeeId.Value && p.Role == InternalConversationParticipantRole.Owner);
            conversation.CanInviteParticipants = conversation.Participants.Any(p => p.EmployeeId == employeeId.Value);
        }
        if (_areas.CanObserveCompanyConversations && !conversation.Participants.Any(p => p.EmployeeId == employeeId.Value))
        {
            conversation.CanManageParticipants = false;
            conversation.CanInviteParticipants = false;
        }
        var roleSnapshot = await timing.RunAsync("thread-roles", () => _areas.ParticipantRolesAsync(conversation.Participants.Select(p => p.EmployeeId).Append(conversation.CreatedBy), cancellationToken));
        conversation.Participants = await timing.RunAsync("thread-visible-members", () => _areas.VisibleParticipantsAsync(conversation.RelatedType, conversation.Participants, cancellationToken, roleSnapshot));
        if (!_areas.CanObserveCompanyConversations &&
            roleSnapshot.Developers.Contains(conversation.CreatedBy))
        {
            conversation.CreatedBy = Guid.Empty;
            conversation.CreatedByName = null;
        }
        if (request.AreaCode != null || conversation.RelatedExternalId == InternalMailAreaAccessService.ResearchMarker)
        {
            var memberIds = conversation.Participants.Select(p => p.EmployeeId).ToArray();
            var eligible = await timing.RunAsync("thread-eligible-members", () => _areas.EligibleParticipantsAsync(companyId.Value, request.ConversationId,
                memberIds, cancellationToken));
            conversation.Participants = conversation.Participants.Where(p => eligible.Contains(p.EmployeeId)).ToArray();
        }
        conversation.Participants = await timing.RunAsync("thread-reader-groups", () => _areas.WithReaderGroupsAsync(conversation.Participants, cancellationToken, roleSnapshot));
        conversation.UnreadCount = conversation.Areas.Single(area => area.ConversationId == conversation.ConversationId).UnreadCount;

        if (parent is not null)
        {
            conversation.RelatedType = parent.RelatedType;
            conversation.RelatedId = parent.RelatedId;
            conversation.RelatedExternalId = parent.RelatedExternalId;
        }

        IReadOnlyDictionary<Guid, SampleRequestConversationInfoDto> sampleRequestInfoById =
            new Dictionary<Guid, SampleRequestConversationInfoDto>();
        IReadOnlyDictionary<Guid, QuotationConversationInfoDto> quotationInfoById =
            new Dictionary<Guid, QuotationConversationInfoDto>();

        if (conversation.RelatedId is { } sampleRequestId &&
            conversation.RelatedType == InternalMailRelatedType.SampleRequest)
        {
            sampleRequestInfoById = await timing.RunAsync("sample-info", () => _sampleRequestInfoResolver.ResolveAsync(
                [sampleRequestId],
                companyId.Value,
                cancellationToken));
        }
        else if (conversation.RelatedId is { } quotationId &&
                 conversation.RelatedType == InternalMailRelatedType.Quotation)
        {
            quotationInfoById = await timing.RunAsync("quotation-info", () => _quotationInfoResolver.ResolveAsync(
                [quotationId],
                companyId.Value,
                cancellationToken));
        }

        InternalConversationRelatedInfoMapper.Apply(conversation, sampleRequestInfoById, quotationInfoById);

        return conversation;
    }
}
