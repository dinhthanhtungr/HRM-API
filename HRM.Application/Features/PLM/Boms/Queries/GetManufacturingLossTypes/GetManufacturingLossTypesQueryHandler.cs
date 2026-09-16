using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossTypes;

internal sealed class GetManufacturingLossTypesQueryHandler
    : IRequestHandler<GetManufacturingLossTypesQuery, IReadOnlyList<ManufacturingLossTypeDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingLossTypesQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ManufacturingLossTypeDto>> Handle(
        GetManufacturingLossTypesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return [];
        }

        return await _dbContext.ManufacturingLossTypes
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive))
            .OrderBy(x => x.Code)
            .Select(x => new ManufacturingLossTypeDto
            {
                ManufacturingLossTypeId = x.ManufacturingLossTypeId,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                DefaultCalculationMethod = x.DefaultCalculationMethod,
                IsRecoverable = x.IsRecoverable,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
