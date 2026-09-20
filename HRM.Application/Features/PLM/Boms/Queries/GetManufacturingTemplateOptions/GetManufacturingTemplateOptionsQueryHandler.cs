using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingTemplateOptions;

internal sealed class GetManufacturingTemplateOptionsQueryHandler : IRequestHandler<GetManufacturingTemplateOptionsQuery, IReadOnlyList<ManufacturingTemplateOptionDto>>
{
    private readonly IPLMReadDbContext _db; private readonly ICurrentUser _currentUser;
    public GetManufacturingTemplateOptionsQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser) { _db = db; _currentUser = currentUser; }
    public async Task<IReadOnlyList<ManufacturingTemplateOptionDto>> Handle(GetManufacturingTemplateOptionsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId) return [];
        var date = request.EffectiveOn ?? DateTime.Now;
        var keyword = request.Keyword?.Trim();
        if (request.IsProcessTemplate)
        {
            var query = _db.ManufacturingProcessTemplates.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive && x.Status == ManufacturingTemplateStatus.Released && (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= date) && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date));
            if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => EF.Functions.ILike(x.ExternalId, $"%{keyword}%") || EF.Functions.ILike(x.Name, $"%{keyword}%"));
            return await query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.VersionNo).Select(x => new ManufacturingTemplateOptionDto { Id = x.ManufacturingProcessTemplateId, ExternalId = x.ExternalId, Name = x.Name, VersionNo = x.VersionNo, StageCount = x.Stages.Count, DisplayName = x.ExternalId + " - V" + x.VersionNo + " - " + x.Name }).ToListAsync(cancellationToken);
        }
        var instructions = _db.ManufacturingWorkInstructionTemplates.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive && x.Status == ManufacturingTemplateStatus.Released && (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= date) && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date));
        if (!string.IsNullOrWhiteSpace(keyword)) instructions = instructions.Where(x => EF.Functions.ILike(x.ExternalId, $"%{keyword}%") || EF.Functions.ILike(x.Name, $"%{keyword}%"));
        return await instructions.OrderBy(x => x.ExternalId).ThenByDescending(x => x.VersionNo).Select(x => new ManufacturingTemplateOptionDto { Id = x.ManufacturingWorkInstructionTemplateId, ExternalId = x.ExternalId, Name = x.Name, VersionNo = x.VersionNo, DisplayName = x.ExternalId + " - V" + x.VersionNo + " - " + x.Name }).ToListAsync(cancellationToken);
    }
}
