using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplates;

internal sealed class GetManufacturingProcessTemplatesQueryHandler : IRequestHandler<GetManufacturingProcessTemplatesQuery, IReadOnlyList<ManufacturingProcessTemplateDto>>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _currentUser;
    
    public GetManufacturingProcessTemplatesQueryHandler(
        IPLMReadDbContext db, 
        ICurrentUser currentUser){ 
            _db = db; 
            _currentUser = currentUser; 
        }
    public async Task<IReadOnlyList<ManufacturingProcessTemplateDto>> Handle(GetManufacturingProcessTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId) return [];
        var query = _db.ManufacturingProcessTemplates.AsNoTracking().AsSplitQuery()
            .Include(x => x.ApplicabilityRules)
                .ThenInclude(x => x.Category)
            .Include(x => x.Stages)
                .ThenInclude(x => x.WorkInstructionTemplate)
            .Include(x => x.Stages)
                .ThenInclude(x => x.Machines)
                .ThenInclude(x => x.Equipment)
            .Include(x => x.Stages)
                .ThenInclude(x => x.Machines)
                .ThenInclude(x => x.Parameters)
            .Include(x => x.StageTransitions)
                .ThenInclude(x => x.FromStage)
            .Include(x => x.StageTransitions)
                .ThenInclude(x => x.ToStage)
            .Where(x => x.CompanyId == companyId && x.IsActive);

        if (request.TemplateId.HasValue) 
            query = query.Where(x => x.ManufacturingProcessTemplateId == request.TemplateId.Value);
        
        if (request.Status.HasValue) 
            query = query.Where(x => x.Status == request.Status.Value);
        
        var entities = await query.OrderBy(x => x.ExternalId)
                                  .ThenByDescending(x => x.VersionNo)
                                  .ToListAsync(cancellationToken);
        
        return entities.Select(ManufacturingTemplateMapper.ToDto).ToList();
    }
}
