using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplates;

internal sealed class GetManufacturingProcessTemplatesQueryHandler : IRequestHandler<GetManufacturingProcessTemplatesQuery, IReadOnlyList<ManufacturingProcessTemplateDto>>
{
    private readonly IPLMReadDbContext _db; private readonly ICurrentUser _currentUser;
    public GetManufacturingProcessTemplatesQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser) { _db = db; _currentUser = currentUser; }
    public async Task<IReadOnlyList<ManufacturingProcessTemplateDto>> Handle(GetManufacturingProcessTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId) return [];
        var query = _db.ManufacturingProcessTemplates.AsNoTracking()
            .Include(x => x.Stages).ThenInclude(x => x.WorkInstructionTemplate)
            .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment)
            .Where(x => x.CompanyId == companyId && x.IsActive);
        if (request.TemplateId.HasValue) query = query.Where(x => x.ManufacturingProcessTemplateId == request.TemplateId.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        var entities = await query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.VersionNo).ToListAsync(cancellationToken);
        return entities.Select(ManufacturingTemplateMapper.ToDto).ToList();
    }
}
