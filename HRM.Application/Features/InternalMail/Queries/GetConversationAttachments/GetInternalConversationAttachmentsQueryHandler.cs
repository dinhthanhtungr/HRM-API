using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationAttachments;

internal sealed class GetInternalConversationAttachmentsQueryHandler
    : IRequestHandler<GetInternalConversationAttachmentsQuery, PagedResult<InternalConversationAttachmentDto>?>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetInternalConversationAttachmentsQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<InternalConversationAttachmentDto>?> Handle(
        GetInternalConversationAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return null;
        }

        var canRead = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        if (!canRead)
        {
            return null;
        }

        var chatAttachments = await _dbContext.InternalMessageAttachments
            .AsNoTracking()
            .Where(x =>
                x.Message.InternalConversationId == request.ConversationId &&
                !x.Message.IsDeleted &&
                x.Attachment.IsActive)
            .Select(x => new InternalConversationAttachmentDto
            {
                AttachmentId = x.AttachmentId,
                Source = "Chat",
                MessageId = x.InternalMessageId,
                SenderEmployeeId = x.Message.SenderEmployeeId,
                SenderName = x.Message.SenderEmployee.FullName,
                SentAt = x.Message.SentAt,
                FileName = x.Attachment.FileName,
                SizeBytes = x.Attachment.SizeBytes
            })
            .ToListAsync(cancellationToken);

        var sampleRequestAttachmentCollectionId = await _dbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.CompanyId == companyId.Value &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId.HasValue)
            .Join(
                _dbContext.SampleRequests.AsNoTracking(),
                conversation => conversation.RelatedId!.Value,
                sampleRequest => sampleRequest.SampleRequestId,
                (_, sampleRequest) => new
                {
                    sampleRequest.AttachmentCollectionId,
                    sampleRequest.CompanyId,
                    sampleRequest.IsActive
                })
            .Where(x => x.IsActive && x.CompanyId == companyId.Value)
            .Select(x => (Guid?)x.AttachmentCollectionId)
            .FirstOrDefaultAsync(cancellationToken);

        var sampleRequestAttachments = sampleRequestAttachmentCollectionId.HasValue
            ? await _dbContext.AttachmentModels
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AttachmentCollectionId == sampleRequestAttachmentCollectionId.Value)
                .Select(x => new InternalConversationAttachmentDto
                {
                    AttachmentId = x.AttachmentId,
                    Source = "SampleRequest",
                    SentAt = x.CreateDate,
                    FileName = x.FileName,
                    SizeBytes = x.SizeBytes
                })
                .ToListAsync(cancellationToken)
            : new List<InternalConversationAttachmentDto>();

        var items = chatAttachments
            .Concat(sampleRequestAttachments)
            .ToList();

        foreach (var item in items)
        {
            InternalMessageAttachmentPresentation.Enrich(item, request.ConversationId);
        }

        var normalizedKind = request.Kind?.Trim();
        if (string.Equals(normalizedKind, "Image", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(x => x.IsImage).ToList();
        }
        else if (string.Equals(normalizedKind, "File", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(x => !x.IsImage).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(normalizedKind) &&
                 !string.Equals(normalizedKind, "All", StringComparison.OrdinalIgnoreCase))
        {
            return new PagedResult<InternalConversationAttachmentDto>(
                Array.Empty<InternalConversationAttachmentDto>(),
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize);
        }

        var totalCount = items.Count;
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        items = items
            .OrderByDescending(x => x.SentAt)
            .ThenByDescending(x => x.AttachmentId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<InternalConversationAttachmentDto>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}
