using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetMessages;

internal sealed class GetInternalMessagesQueryHandler
    : IRequestHandler<GetInternalMessagesQuery, PagedResult<InternalMessageDto>?>
{
    private const int MaxReplyPreviewLength = 300;
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetInternalMessagesQueryHandler(IInternalMailDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<InternalMessageDto>?> Handle(
        GetInternalMessagesQuery request,
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

        var messageQuery = _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x => x.InternalConversationId == request.ConversationId);

        var totalCount = await messageQuery.CountAsync(cancellationToken);
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;

        var items = await messageQuery
            .OrderByDescending(x => x.SentAt)
            .ThenByDescending(x => x.InternalMessageId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InternalMessageDto
            {
                MessageId = x.InternalMessageId,
                ConversationId = x.InternalConversationId,
                SenderEmployeeId = x.SenderEmployeeId,
                SenderName = x.SenderEmployee.FullName,
                MessageType = x.MessageType,
                Body = x.IsDeleted ? string.Empty : x.Body,
                PayloadJson = x.IsDeleted ? null : x.PayloadJson,
                ReplyToMessageId = x.ReplyToMessageId,
                ReplyTo = x.ReplyToMessageId == null ? null : new InternalMessageReplyDto
                {
                    MessageId = x.ReplyToMessage!.InternalMessageId,
                    SenderEmployeeId = x.ReplyToMessage.SenderEmployeeId,
                    SenderName = x.ReplyToMessage.SenderEmployee.FullName,
                    BodyPreview = x.ReplyToMessage.IsDeleted
                        ? string.Empty
                        : x.ReplyToMessage.Body.Length <= MaxReplyPreviewLength
                            ? x.ReplyToMessage.Body
                            : x.ReplyToMessage.Body.Substring(0, MaxReplyPreviewLength),
                    MessageType = x.ReplyToMessage.MessageType,
                    IsDeleted = x.ReplyToMessage.IsDeleted
                },
                IsUrgent = x.IsUrgent,
                SentAt = x.SentAt,
                IsEdited = x.IsEdited,
                EditedAt = x.EditedAt,
                IsDeleted = x.IsDeleted,
                IsRead = x.ReadStates
                    .Where(state => state.EmployeeId == employeeId.Value)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadAt = x.ReadStates
                    .Where(state => state.EmployeeId == employeeId.Value)
                    .Select(state => state.ReadAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var messageIds = items.Select(x => x.MessageId).ToArray();
        if (messageIds.Length > 0)
        {
            var references = await _dbContext.InternalMessageReferences
                .AsNoTracking()
                .Where(x => messageIds.Contains(x.InternalMessageId))
                .Select(x => new
                {
                    x.InternalMessageId,
                    Item = new InternalMessageReferenceDto
                    {
                        ReferenceId = x.InternalMessageReferenceId,
                        RelatedType = x.RelatedType,
                        RelatedId = x.RelatedId,
                        RelatedExternalId = x.RelatedExternalId,
                        RelatedNameSnapshot = x.RelatedNameSnapshot,
                        SnapshotJson = x.SnapshotJson,
                        IsPrimary = x.IsPrimary
                    }
                })
                .ToListAsync(cancellationToken);


            var attachments = await _dbContext.InternalMessageAttachments
                .AsNoTracking()
                .Where(x => messageIds.Contains(x.InternalMessageId) && x.Attachment.IsActive)
                .Select(x => new
                {
                    x.InternalMessageId,
                    Item = new InternalMessageAttachmentDto
                    {
                        AttachmentId = x.AttachmentId,
                        FileName = x.Attachment.FileName,
                        SizeBytes = x.Attachment.SizeBytes
                    }
                })
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                item.References = references
                    .Where(x => x.InternalMessageId == item.MessageId)
                    .Select(x => x.Item)
                    .ToList();
                var messageAttachments = attachments
                    .Where(x => x.InternalMessageId == item.MessageId)
                    .Select(x => x.Item)
                    .ToList();
                foreach (var attachment in messageAttachments)
                {
                    InternalMessageAttachmentPresentation.Enrich(attachment);
                }

                item.Attachments = messageAttachments;
            }
        }

        return new PagedResult<InternalMessageDto>(items, totalCount, pageNumber, pageSize);
    }
}
