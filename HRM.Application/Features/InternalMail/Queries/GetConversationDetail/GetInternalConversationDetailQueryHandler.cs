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
    private readonly IInternalConversationAccessService _conversationAccessService;
    private readonly InternalConversationSampleRequestInfoResolver _sampleRequestInfoResolver;
    private readonly InternalConversationQuotationInfoResolver _quotationInfoResolver;

    public GetInternalConversationDetailQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IInternalConversationAccessService conversationAccessService,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
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

        var conversation = await _dbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.CompanyId == companyId.Value &&
                x.IsActive)
            .Select(x => new InternalConversationDetailDto
            {
                ConversationId = x.InternalConversationId,
                Subject = x.Subject,
                RelatedType = x.RelatedType,
                RelatedId = x.RelatedId,
                RelatedExternalId = x.RelatedExternalId,
                CreatedBy = x.CreatedBy,
                CreatedByName = x.CreatedByNavigation.FullName,
                CreatedAt = x.CreatedAt,
                LastMessageAt = x.LastMessageAt,
                LastMessageId = x.LastMessageId,
                IsArchived = x.Participants
                    .Where(participant => participant.EmployeeId == employeeId.Value && participant.IsActive)
                    .Select(participant => participant.IsArchived)
                    .FirstOrDefault(),
                IsMuted = x.Participants
                    .Where(participant => participant.EmployeeId == employeeId.Value && participant.IsActive)
                    .Select(participant => participant.IsMuted)
                    .FirstOrDefault(),
                LastReadAt = x.Participants
                    .Where(participant => participant.EmployeeId == employeeId.Value && participant.IsActive)
                    .Select(participant => participant.LastReadAt)
                    .FirstOrDefault(),
                Participants = x.Participants
                    .Where(participant => participant.IsActive)
                    .OrderBy(participant => participant.JoinedAt)
                    .Select(participant => new InternalConversationParticipantDto
                    {
                        EmployeeId = participant.EmployeeId,
                        EmployeeName = participant.Employee.FullName,
                        Role = participant.Role,
                        JoinedAt = participant.JoinedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return null;
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
