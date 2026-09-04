using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetAttachmentContent;

internal sealed class GetInternalMailAttachmentContentQueryHandler
    : IRequestHandler<GetInternalMailAttachmentContentQuery, AttachmentContent?>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly IImageThumbnailGenerator _thumbnailGenerator;

    public GetInternalMailAttachmentContentQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IFileStorage fileStorage,
        IImageThumbnailGenerator thumbnailGenerator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _thumbnailGenerator = thumbnailGenerator;
    }

    public async Task<AttachmentContent?> Handle(
        GetInternalMailAttachmentContentQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.AttachmentId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return null;
        }

        var attachment = await _dbContext.InternalMessageAttachments
            .AsNoTracking()
            .Where(x =>
                x.AttachmentId == request.AttachmentId &&
                x.Attachment.IsActive &&
                !x.Message.IsDeleted &&
                x.Message.Conversation.CompanyId == companyId.Value &&
                x.Message.Conversation.IsActive &&
                (!request.ConversationId.HasValue ||
                 x.Message.InternalConversationId == request.ConversationId.Value) &&
                x.Message.Conversation.Participants.Any(participant =>
                    participant.EmployeeId == employeeId.Value && participant.IsActive))
            .Select(x => new AttachmentFile(
                x.Attachment.StoragePath,
                x.Attachment.FileName,
                x.Attachment.FileName.ToLower().EndsWith(".png") ||
                x.Attachment.FileName.ToLower().EndsWith(".jpg") ||
                x.Attachment.FileName.ToLower().EndsWith(".jpeg") ||
                x.Attachment.FileName.ToLower().EndsWith(".gif") ||
                x.Attachment.FileName.ToLower().EndsWith(".webp") ||
                x.Attachment.FileName.ToLower().EndsWith(".bmp")))
            .FirstOrDefaultAsync(cancellationToken);

        if (attachment is null && request.ConversationId.HasValue)
        {
            attachment = await (
                    from conversation in _dbContext.InternalConversations.AsNoTracking()
                    join sampleRequest in _dbContext.SampleRequests.AsNoTracking()
                        on conversation.RelatedId!.Value equals sampleRequest.SampleRequestId
                    join sampleAttachment in _dbContext.AttachmentModels.AsNoTracking()
                        on sampleRequest.AttachmentCollectionId equals sampleAttachment.AttachmentCollectionId
                    where conversation.InternalConversationId == request.ConversationId.Value &&
                          conversation.CompanyId == companyId.Value &&
                          conversation.IsActive &&
                          conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
                          sampleRequest.CompanyId == companyId.Value &&
                          sampleRequest.IsActive &&
                          sampleAttachment.AttachmentId == request.AttachmentId &&
                          sampleAttachment.IsActive &&
                          conversation.Participants.Any(participant =>
                              participant.EmployeeId == employeeId.Value && participant.IsActive)
                    select new AttachmentFile(
                        sampleAttachment.StoragePath,
                        sampleAttachment.FileName,
                        sampleAttachment.FileName.ToLower().EndsWith(".png") ||
                        sampleAttachment.FileName.ToLower().EndsWith(".jpg") ||
                        sampleAttachment.FileName.ToLower().EndsWith(".jpeg") ||
                        sampleAttachment.FileName.ToLower().EndsWith(".gif") ||
                        sampleAttachment.FileName.ToLower().EndsWith(".webp") ||
                        sampleAttachment.FileName.ToLower().EndsWith(".bmp")))
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (attachment is null)
        {
            return null;
        }

        if (request.Thumbnail)
        {
            if (!attachment.IsImage)
            {
                return null;
            }

            var thumbnailPath = InternalMailThumbnailStorage.GetPath(
                attachment.StoragePath,
                request.AttachmentId);
            try
            {
                var thumbnail = await _fileStorage.OpenReadAsync(thumbnailPath, cancellationToken);
                return new AttachmentContent(
                    thumbnail.Stream,
                    "image/webp",
                    $"{request.AttachmentId:N}.webp",
                    thumbnail.Length);
            }
            catch (FileNotFoundException)
            {
                var original = await _fileStorage.OpenReadAsync(
                    attachment.StoragePath,
                    cancellationToken);
                Stream? generated;
                await using (original.Stream)
                {
                    generated = await _thumbnailGenerator.GenerateWebpAsync(
                        original.Stream,
                        InternalMailThumbnailStorage.MaxWidth,
                        InternalMailThumbnailStorage.MaxHeight,
                        cancellationToken);
                }

                if (generated is null)
                {
                    return null;
                }

                await _fileStorage.SaveAtPathAsync(generated, thumbnailPath, cancellationToken);
                generated.Position = 0;
                return new AttachmentContent(
                    generated,
                    "image/webp",
                    $"{request.AttachmentId:N}.webp",
                    generated.Length);
            }
        }

        var file = await _fileStorage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        return new AttachmentContent(file.Stream, file.ContentType, attachment.FileName, file.Length);
    }

    private sealed record AttachmentFile(string StoragePath, string FileName, bool IsImage);
}
