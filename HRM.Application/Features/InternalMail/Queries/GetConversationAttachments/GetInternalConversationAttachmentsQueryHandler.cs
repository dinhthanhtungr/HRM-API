using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
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

        var query = _dbContext.InternalMessageAttachments
            .AsNoTracking()
            .Where(x =>
                x.Message.InternalConversationId == request.ConversationId &&
                !x.Message.IsDeleted &&
                x.Attachment.IsActive);

        var normalizedKind = request.Kind?.Trim();
        if (string.Equals(normalizedKind, "Image", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x =>
                x.Attachment.FileName.ToLower().EndsWith(".png") ||
                x.Attachment.FileName.ToLower().EndsWith(".jpg") ||
                x.Attachment.FileName.ToLower().EndsWith(".jpeg") ||
                x.Attachment.FileName.ToLower().EndsWith(".gif") ||
                x.Attachment.FileName.ToLower().EndsWith(".webp") ||
                x.Attachment.FileName.ToLower().EndsWith(".bmp"));
        }
        else if (string.Equals(normalizedKind, "File", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x =>
                !x.Attachment.FileName.ToLower().EndsWith(".png") &&
                !x.Attachment.FileName.ToLower().EndsWith(".jpg") &&
                !x.Attachment.FileName.ToLower().EndsWith(".jpeg") &&
                !x.Attachment.FileName.ToLower().EndsWith(".gif") &&
                !x.Attachment.FileName.ToLower().EndsWith(".webp") &&
                !x.Attachment.FileName.ToLower().EndsWith(".bmp"));
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

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var items = await query
            .OrderByDescending(x => x.Message.SentAt)
            .ThenByDescending(x => x.InternalMessageAttachmentId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InternalConversationAttachmentDto
            {
                AttachmentId = x.AttachmentId,
                MessageId = x.InternalMessageId,
                SenderEmployeeId = x.Message.SenderEmployeeId,
                SenderName = x.Message.SenderEmployee.FullName,
                SentAt = x.Message.SentAt,
                FileName = x.Attachment.FileName,
                SizeBytes = x.Attachment.SizeBytes
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            InternalMessageAttachmentPresentation.Enrich(item);
        }

        return new PagedResult<InternalConversationAttachmentDto>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}
