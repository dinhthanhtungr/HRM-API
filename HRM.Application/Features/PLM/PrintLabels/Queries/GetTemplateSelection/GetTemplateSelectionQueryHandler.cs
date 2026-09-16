using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.PrintLabels.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.PrintLabels.Queries.GetTemplateSelection;

internal sealed class GetTemplateSelectionQueryHandler
    : IRequestHandler<GetTemplateSelectionQuery, PrintLabelTemplateSelectionDto?>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _user;

    public GetTemplateSelectionQueryHandler(IPLMReadDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public Task<PrintLabelTemplateSelectionDto?> Handle(
        GetTemplateSelectionQuery request,
        CancellationToken ct)
    {
        var companyId = _user.CompanyId.GetValueOrDefault();
        if (companyId == Guid.Empty || request.PrintLabelTemplateId == Guid.Empty)
        {
            return Task.FromResult<PrintLabelTemplateSelectionDto?>(null);
        }

        return _db.PrintLabelTemplates  
            .AsNoTracking()
            .Where(template =>
                template.Id == request.PrintLabelTemplateId &&
                template.CompanyId == companyId &&
                template.IsActive)
            .Select(template => new PrintLabelTemplateSelectionDto
            {
                Id = template.Id,
                ExternalId = template.ExternalId,
                Name = template.Name,
                WidthMm = template.WidthMm,
                HeightMm = template.HeightMm,
                AttachmentCollectionId = template.AttachmentCollectionId,
                Logos = template.TemplateLogos
                    .Where(link => link.IsActive && link.Logo.IsActive && link.Logo.CompanyId == companyId)
                    .OrderBy(link => link.SortOrder)
                    .Select(link => new PrintLabelLogoDto
                    {
                        Id = link.Logo.Id,
                        ExternalId = link.Logo.ExternalId,
                        Name = link.Logo.Name,
                        AttachmentCollectionId = link.Logo.AttachmentCollectionId,
                        IsDefault = link.IsDefault
                    })
                    .ToList(),
                Elements = template.Elements
                    .Where(element => element.IsActive)
                    .OrderBy(element => element.LineNo)
                    .Select(element => new PrintLabelElementDto
                    {
                        Id = element.Id,
                        LineNo = element.LineNo,
                        FieldKey = element.FieldKey,
                        DefaultValue = element.DefaultValue
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }
}
