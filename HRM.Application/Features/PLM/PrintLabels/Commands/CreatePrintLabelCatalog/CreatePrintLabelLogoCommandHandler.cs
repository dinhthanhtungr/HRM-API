using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.PrintectSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.PrintLabels.Commands.CreatePrintLabelCatalog;

internal sealed class CreatePrintLabelLogoCommandHandler
    : IRequestHandler<CreatePrintLabelLogoCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDateTimeProvider _clock;

    public CreatePrintLabelLogoCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser user,
        IDateTimeProvider clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task<OperationResult<Guid>> Handle(
        CreatePrintLabelLogoCommand request,
        CancellationToken ct)
    {
        var companyId = _user.CompanyId.GetValueOrDefault();
        if (companyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            request.AttachmentCollectionId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Code, Name, AttachmentCollectionId, and current company are required.");
        }

        var collectionExists = await _db.AttachmentCollections.AsNoTracking()
            .AnyAsync(x => x.AttachmentCollectionId == request.AttachmentCollectionId, ct);
        if (!collectionExists)
        {
            return OperationResult<Guid>.Fail("Attachment collection was not found.");
        }

        var code = request.Code.Trim();
        var duplicateExists = await _db.PrintLabelLogos
            .AnyAsync(x => x.CompanyId == companyId && x.ExternalId == code, ct);
        if (duplicateExists)
        {
            return OperationResult<Guid>.Fail("Logo code already exists.");
        }

        var now = _clock.Now;
        var entity = new PrintLabelLogo
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            ExternalId = code,
            Name = request.Name.Trim(),
            AttachmentCollectionId = request.AttachmentCollectionId,
            CreatedBy = _user.EmployeeId,
            CreatedDate = now,
            UpdatedBy = _user.EmployeeId,
            UpdatedDate = now
        };
        await _db.PrintLabelLogos.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return OperationResult<Guid>.Ok(entity.Id, "Created print label logo.");
    }
}
