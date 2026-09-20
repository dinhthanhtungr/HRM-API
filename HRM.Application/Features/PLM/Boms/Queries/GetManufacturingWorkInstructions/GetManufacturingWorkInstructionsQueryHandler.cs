using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingWorkInstructions;

internal sealed class GetManufacturingWorkInstructionsQueryHandler : IRequestHandler<GetManufacturingWorkInstructionsQuery, IReadOnlyList<ManufacturingWorkInstructionTemplateDto>>
{
    private readonly IPLMReadDbContext _db; private readonly ICurrentUser _currentUser;
    public GetManufacturingWorkInstructionsQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser) { _db = db; _currentUser = currentUser; }
    public async Task<IReadOnlyList<ManufacturingWorkInstructionTemplateDto>> Handle(GetManufacturingWorkInstructionsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId) return [];
        var query = _db.ManufacturingWorkInstructionTemplates.AsNoTracking().Include(x => x.ChecklistItems).Where(x => x.CompanyId == companyId && x.IsActive);
        if (request.TemplateId.HasValue) query = query.Where(x => x.ManufacturingWorkInstructionTemplateId == request.TemplateId.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        var entities = await query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.VersionNo).ToListAsync(cancellationToken);
        return entities.Select(ManufacturingTemplateMapper.ToDto).ToList();
    }
}
