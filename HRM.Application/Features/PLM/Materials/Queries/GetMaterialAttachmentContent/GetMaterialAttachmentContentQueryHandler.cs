using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialAttachmentContent;

internal sealed class GetMaterialAttachmentContentQueryHandler
    : IRequestHandler<GetMaterialAttachmentContentQuery, AttachmentContent?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAttachmentService _attachmentService;

    public GetMaterialAttachmentContentQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IAttachmentService attachmentService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _attachmentService = attachmentService;
    }

    public async Task<AttachmentContent?> Handle(
        GetMaterialAttachmentContentQuery request,
        CancellationToken cancellationToken)
    {
        if (request.MaterialId == Guid.Empty ||
            request.AttachmentId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        var canAccess = await (
                from material in _dbContext.Materials.AsNoTracking()
                join attachment in _dbContext.AttachmentModels.AsNoTracking()
                    on material.AttachmentCollectionId equals attachment.AttachmentCollectionId
                where material.MaterialId == request.MaterialId &&
                      material.CompanyId == companyId &&
                      material.IsActive == true &&
                      attachment.AttachmentId == request.AttachmentId &&
                      attachment.IsActive
                select attachment.AttachmentId)
            .AnyAsync(cancellationToken);

        if (!canAccess)
        {
            return null;
        }

        try
        {
            return await _attachmentService.GetContentAsync(
                request.AttachmentId,
                cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
