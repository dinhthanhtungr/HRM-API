using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.PrintectSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.PrintLabels.Commands.CreatePrintLabelTemplate;

internal sealed class CreatePrintLabelTemplateCommandHandler
    : IRequestHandler<CreatePrintLabelTemplateCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDateTimeProvider _clock;

    public CreatePrintLabelTemplateCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser user,
        IDateTimeProvider clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task<OperationResult<Guid>> Handle(
        CreatePrintLabelTemplateCommand request,
        CancellationToken ct)
    {
        var companyId = _user.CompanyId.GetValueOrDefault();
        if (companyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.ExternalId) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            request.WidthMm <= 0 ||
            request.HeightMm <= 0)
        {
            return OperationResult<Guid>.Fail(
                "ExternalId, Name, positive dimensions, and current company are required.");
        }

        var externalId = request.ExternalId.Trim();
        var duplicateExists = await _db.PrintLabelTemplates
            .AnyAsync(x => x.CompanyId == companyId && x.ExternalId == externalId, ct);
        if (duplicateExists)
        {
            return OperationResult<Guid>.Fail("Template external ID already exists.");
        }

        var logoIds = request.PrintLabelLogoIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        var validLogoCount = await _db.PrintLabelLogos.CountAsync(
            x => x.CompanyId == companyId && x.IsActive && logoIds.Contains(x.Id),
            ct);
        if (logoIds.Length != request.PrintLabelLogoIds.Count ||
            validLogoCount != logoIds.Length)
        {
            return OperationResult<Guid>.Fail("One or more selected logos are invalid.");
        }

        var hasInvalidElements = request.Elements.GroupBy(x => x.LineNo)
            .Any(x => x.Key <= 0 || x.Count() > 1) ||
            request.Elements.Any(x => string.IsNullOrWhiteSpace(x.FieldKey)) ||
            request.Elements.GroupBy(
                    x => x.FieldKey.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Any(x => x.Count() > 1);
        if (hasInvalidElements)
        {
            return OperationResult<Guid>.Fail(
                "Each element needs a unique positive LineNo and FieldKey.");
        }

        var now = _clock.Now;
        var entity = new PrintLabelTemplate
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            ExternalId = externalId,
            Name = request.Name.Trim(),
            LabelType = request.LabelType?.Trim(),
            Instructions = request.Instructions?.Trim(),
            WidthMm = request.WidthMm,
            HeightMm = request.HeightMm,
            AttachmentCollectionId = request.AttachmentCollectionId,
            CreatedBy = _user.EmployeeId,
            CreatedDate = now,
            UpdatedBy = _user.EmployeeId,
            UpdatedDate = now,
            Elements = request.Elements.Select(x => new PrintLabelElement
            {
                Id = Guid.CreateVersion7(),
                LineNo = x.LineNo,
                FieldKey = x.FieldKey.Trim(),
                DefaultValue = x.DefaultValue?.Trim()
            }).ToList(),
            TemplateLogos = logoIds.Select((id, index) => new PrintLabelTemplateLogo
            {
                Id = Guid.CreateVersion7(),
                PrintLabelLogoId = id,
                SortOrder = index + 1,
                IsDefault = index == 0
            }).ToList()
        };

        await _db.PrintLabelTemplates.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return OperationResult<Guid>.Ok(entity.Id, "Created print label template.");
    }
}
