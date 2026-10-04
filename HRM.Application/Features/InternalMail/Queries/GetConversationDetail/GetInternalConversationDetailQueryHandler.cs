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
    private readonly IInternalConversationAccessService _conversationAccessService;
    private readonly InternalConversationSampleRequestInfoResolver _sampleRequestInfoResolver;
    private readonly InternalConversationQuotationInfoResolver _quotationInfoResolver;

    public GetInternalConversationDetailQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IInternalConversationAccessService conversationAccessService,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver,
        InternalMailAreaAccessService areas)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _conversationAccessService = conversationAccessService;
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

        if (!await _conversationAccessService.CanReadAsync(request.ConversationId, cancellationToken))
        {
            return null;
        }

        var conversation = await _areas.Conversations()
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.CompanyId == companyId.Value &&
                x.IsActive)
            .Select(InternalConversationDetailProjection.ForEmployee(employeeId.Value, _dbContext.InternalMessages))
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        conversation.Areas = await _areas.DescribeAsync(request.ConversationId, cancellationToken);
        conversation.AreaCode = InternalMailAreaAccessService.AreaOf(conversation.RelatedType);
        conversation.GroupConversationId = InternalMailAreaAccessService.IsPrivateArea(conversation.RelatedType)
            ? conversation.RelatedId!.Value : conversation.ConversationId;
        if (InternalMailAreaAccessService.IsPrivateArea(conversation.RelatedType))
            conversation.CanManageParticipants = conversation.Participants.Any(p => p.EmployeeId == employeeId.Value &&
                p.Role == InternalConversationParticipantRole.Owner);
        if (request.AreaCode != null)
        {
            if (request.AreaCode != InternalMailAreaAccessService.AreaOf(conversation.RelatedType)) return null;
            var eligible = await _areas.Recipients(companyId.Value, request.AreaCode, request.ConversationId).ToHashSetAsync(cancellationToken);
            conversation.Participants = conversation.Participants.Where(p => eligible.Contains(p.EmployeeId)).ToArray();
        }
        var notificationCount = await _areas.NotificationUnreadCounts()
            .Where(item => item.ConversationId == request.ConversationId)
            .Select(item => item.UnreadCount)
            .FirstOrDefaultAsync(cancellationToken);
        conversation.UnreadCount = Math.Max(conversation.UnreadCount, notificationCount);

        if (InternalMailAreaAccessService.IsPrivateArea(conversation.RelatedType))
        {
            var parent = await _dbContext.InternalConversations.AsNoTracking().FirstAsync(c =>
                c.InternalConversationId == conversation.GroupConversationId && c.CompanyId == companyId.Value, cancellationToken);
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
            sampleRequestInfoById = await _sampleRequestInfoResolver.ResolveAsync(
                [sampleRequestId],
                companyId.Value,
                cancellationToken);
        }
        else if (conversation.RelatedId is { } quotationId &&
                 conversation.RelatedType == InternalMailRelatedType.Quotation)
        {
            quotationInfoById = await _quotationInfoResolver.ResolveAsync(
                [quotationId],
                companyId.Value,
                cancellationToken);
        }

        InternalConversationRelatedInfoMapper.Apply(conversation, sampleRequestInfoById, quotationInfoById);

        return conversation;
    }
}
