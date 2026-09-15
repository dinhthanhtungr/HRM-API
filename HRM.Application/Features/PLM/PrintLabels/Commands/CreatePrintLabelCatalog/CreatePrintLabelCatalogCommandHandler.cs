using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.PrintectSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.PrintLabels.Commands.CreatePrintLabelCatalog;

internal sealed class CreatePrintLabelLogoCommandHandler : IRequestHandler<CreatePrintLabelLogoCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _user; private readonly IDateTimeProvider _clock;
    public CreatePrintLabelLogoCommandHandler(IPLMWriteDbContext db, ICurrentUser user, IDateTimeProvider clock) => (_db, _user, _clock) = (db, user, clock);
    public async Task<OperationResult<Guid>> Handle(CreatePrintLabelLogoCommand request, CancellationToken ct)
    {
        var companyId = _user.CompanyId.GetValueOrDefault();
        if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || request.AttachmentCollectionId == Guid.Empty) return OperationResult<Guid>.Fail("Code, Name, AttachmentCollectionId, and current company are required.");
        if (!await _db.AttachmentCollections.AsNoTracking().AnyAsync(x => x.AttachmentCollectionId == request.AttachmentCollectionId, ct)) return OperationResult<Guid>.Fail("Attachment collection was not found.");
        var code = request.Code.Trim();
        if (await _db.PrintLabelLogos.AnyAsync(x => x.CompanyId == companyId && x.Code == code, ct)) return OperationResult<Guid>.Fail("Logo code already exists.");
        var entity = new PrintLabelLogo { Id = Guid.CreateVersion7(), CompanyId = companyId, Code = code, Name = request.Name.Trim(), AttachmentCollectionId = request.AttachmentCollectionId, CreatedBy = _user.EmployeeId, CreatedDate = _clock.Now, UpdatedBy = _user.EmployeeId, UpdatedDate = _clock.Now };
        await _db.PrintLabelLogos.AddAsync(entity, ct); await _db.SaveChangesAsync(ct); return OperationResult<Guid>.Ok(entity.Id, "Created print label logo.");
    }
}

internal sealed class CreatePrintLabelTemplateCommandHandler : IRequestHandler<CreatePrintLabelTemplateCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _user; private readonly IDateTimeProvider _clock;
    public CreatePrintLabelTemplateCommandHandler(IPLMWriteDbContext db, ICurrentUser user, IDateTimeProvider clock) => (_db, _user, _clock) = (db, user, clock);
    public async Task<OperationResult<Guid>> Handle(CreatePrintLabelTemplateCommand request, CancellationToken ct)
    {
        var companyId = _user.CompanyId.GetValueOrDefault();
        if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || request.WidthMm <= 0 || request.HeightMm <= 0) return OperationResult<Guid>.Fail("Code, Name, positive dimensions, and current company are required.");
        var code = request.Code.Trim();
        if (await _db.PrintLabelTemplates.AnyAsync(x => x.CompanyId == companyId && x.Code == code, ct)) return OperationResult<Guid>.Fail("Template code already exists.");
        var logoIds = request.PrintLabelLogoIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (logoIds.Length != request.PrintLabelLogoIds.Count || await _db.PrintLabelLogos.CountAsync(x => x.CompanyId == companyId && x.IsActive && logoIds.Contains(x.Id), ct) != logoIds.Length) return OperationResult<Guid>.Fail("One or more selected logos are invalid.");
        if (request.Elements.GroupBy(x => x.LineNo).Any(x => x.Key <= 0 || x.Count() > 1) || request.Elements.Any(x => string.IsNullOrWhiteSpace(x.ElementType))) return OperationResult<Guid>.Fail("Each element needs a unique positive LineNo and ElementType.");
        var entity = new PrintLabelTemplate { Id = Guid.CreateVersion7(), CompanyId = companyId, Code = code, Name = request.Name.Trim(), LabelType = request.LabelType?.Trim(), Instructions = request.Instructions?.Trim(), WidthMm = request.WidthMm, HeightMm = request.HeightMm, AttachmentCollectionId = request.AttachmentCollectionId, CreatedBy = _user.EmployeeId, CreatedDate = _clock.Now, UpdatedBy = _user.EmployeeId, UpdatedDate = _clock.Now };
        entity.Elements = request.Elements.Select(x => new PrintLabelElement { Id = Guid.CreateVersion7(), LineNo = x.LineNo, ElementType = x.ElementType.Trim(), FieldKey = x.FieldKey?.Trim(), DisplayName = x.DisplayName?.Trim(), ValueSource = x.ValueSource?.Trim(), DefaultValue = x.DefaultValue?.Trim(), PrefixText = x.PrefixText?.Trim(), IsRequired = x.IsRequired, IsEditableBySales = x.IsEditableBySales, X = x.X, Y = x.Y, Width = x.Width, Height = x.Height }).ToList();
        entity.TemplateLogos = logoIds.Select((id, index) => new PrintLabelTemplateLogo { Id = Guid.CreateVersion7(), PrintLabelLogoId = id, SortOrder = index + 1, IsDefault = index == 0 }).ToList();
        await _db.PrintLabelTemplates.AddAsync(entity, ct); await _db.SaveChangesAsync(ct); return OperationResult<Guid>.Ok(entity.Id, "Created print label template.");
    }
}
