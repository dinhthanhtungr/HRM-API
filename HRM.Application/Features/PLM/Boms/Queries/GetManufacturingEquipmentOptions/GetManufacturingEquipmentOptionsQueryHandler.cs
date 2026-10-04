using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Searching;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingEquipmentOptions;

internal sealed class GetManufacturingEquipmentOptionsQueryHandler
    : IRequestHandler<GetManufacturingEquipmentOptionsQuery, PagedResult<ManufacturingEquipmentOptionDto>>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingEquipmentOptionsQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ManufacturingEquipmentOptionDto>> Handle(
        GetManufacturingEquipmentOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return new PagedResult<ManufacturingEquipmentOptionDto>([], 0, request.NormalizedPage, request.NormalizedPageSize);

        var query = _db.EquipmentsMro.AsNoTracking().Where(x => x.FactoryId == companyId);
        var keyword = request.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
            query = query.Where(x =>
                EF.Functions.ILike(x.EquipmentExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.EquipmentName, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var groupType = request.GroupType?.Trim();
        if (!string.IsNullOrWhiteSpace(groupType))
        {
            var pattern = PostgresSearchPattern.ExactLiteral(groupType);
            query = query.Where(x => EF.Functions.ILike(x.GroupType ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var areaExternalId = request.AreaExternalId?.Trim();
        if (!string.IsNullOrWhiteSpace(areaExternalId))
        {
            var pattern = PostgresSearchPattern.ExactLiteral(areaExternalId);
            query = query.Where(x => EF.Functions.ILike(x.AreaExternalId, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var projected = query
            .OrderBy(x => x.EquipmentExternalId)
            .ThenBy(x => x.EquipmentName)
            .Select(x => new ManufacturingEquipmentOptionDto
            {
                EquipmentId = x.EquipmentId,
                EquipmentExternalId = x.EquipmentExternalId,
                EquipmentName = x.EquipmentName,
                GroupType = x.GroupType,
                AreaId = x.AreaId,
                AreaExternalId = x.AreaExternalId,
                PartId = x.PartId,
                PartExternalId = x.PartExternalId,
                DisplayName = x.EquipmentExternalId + " - " + x.EquipmentName
            });

        return await projected.ToPagedResultAsync(request.NormalizedPage, request.NormalizedPageSize, cancellationToken);
    }
}
