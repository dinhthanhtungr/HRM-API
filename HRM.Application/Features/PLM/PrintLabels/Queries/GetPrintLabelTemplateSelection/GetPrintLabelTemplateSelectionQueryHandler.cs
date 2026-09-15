using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.PrintLabels.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.PrintLabels.Queries.GetPrintLabelTemplateSelection;

internal sealed class GetPrintLabelTemplateSelectionQueryHandler : IRequestHandler<GetPrintLabelTemplateSelectionQuery, PrintLabelTemplateSelectionDto?>
{
    private readonly IPLMReadDbContext _db; private readonly ICurrentUser _user;
    public GetPrintLabelTemplateSelectionQueryHandler(IPLMReadDbContext db, ICurrentUser user) => (_db, _user) = (db, user);
    public Task<PrintLabelTemplateSelectionDto?> Handle(GetPrintLabelTemplateSelectionQuery request, CancellationToken ct)
    {
        var companyId = _user.CompanyId.GetValueOrDefault();
        if (companyId == Guid.Empty || request.PrintLabelTemplateId == Guid.Empty) return Task.FromResult<PrintLabelTemplateSelectionDto?>(null);
        return _db.PrintLabelTemplates.AsNoTracking().Where(x => x.Id == request.PrintLabelTemplateId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new PrintLabelTemplateSelectionDto
            {
                Id = x.Id, Code = x.Code, Name = x.Name, WidthMm = x.WidthMm, HeightMm = x.HeightMm, AttachmentCollectionId = x.AttachmentCollectionId,
                Logos = x.TemplateLogos.Where(link => link.IsActive && link.Logo.IsActive && link.Logo.CompanyId == companyId).OrderBy(link => link.SortOrder).Select(link => new PrintLabelLogoDto { Id = link.Logo.Id, Code = link.Logo.Code, Name = link.Logo.Name, AttachmentCollectionId = link.Logo.AttachmentCollectionId, IsDefault = link.IsDefault }).ToList(),
                Elements = x.Elements.Where(element => element.IsActive).OrderBy(element => element.LineNo).Select(element => new PrintLabelElementDto { Id = element.Id, LineNo = element.LineNo, ElementType = element.ElementType, FieldKey = element.FieldKey, DisplayName = element.DisplayName, ValueSource = element.ValueSource, DefaultValue = element.DefaultValue, PrefixText = element.PrefixText, IsRequired = element.IsRequired, IsEditableBySales = element.IsEditableBySales }).ToList()
            }).FirstOrDefaultAsync(ct);
    }
}
