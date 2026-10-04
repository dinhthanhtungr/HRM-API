using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingEquipmentFilterOptions;

internal sealed class GetManufacturingEquipmentFilterOptionsQueryHandler
    : IRequestHandler<GetManufacturingEquipmentFilterOptionsQuery, ManufacturingEquipmentFilterOptionsDto>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingEquipmentFilterOptionsQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ManufacturingEquipmentFilterOptionsDto> Handle(
        GetManufacturingEquipmentFilterOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return new ManufacturingEquipmentFilterOptionsDto();

        var companyEquipment = _db.EquipmentsMro.AsNoTracking().Where(x => x.FactoryId == companyId);
        var groupTypes = await companyEquipment
            .Where(x => x.GroupType != null && x.GroupType != string.Empty)
            .Select(x => x.GroupType!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
         
        var areaRows = await companyEquipment
            .Where(x => x.AreaExternalId != string.Empty)
            .Select(x => new
            {
                ExternalId = x.AreaExternalId,
                Name = x.Area != null ? x.Area.AreaName : x.AreaExternalId
            })
            .Distinct()
            .OrderBy(x => x.ExternalId)
            .ToListAsync(cancellationToken);

        var areas = areaRows
            .Select(x => new ManufacturingEquipmentAreaOptionDto { ExternalId = x.ExternalId, Name = x.Name })
            .ToList();

        return new ManufacturingEquipmentFilterOptionsDto { GroupTypes = groupTypes, Areas = areas };
    }
}
