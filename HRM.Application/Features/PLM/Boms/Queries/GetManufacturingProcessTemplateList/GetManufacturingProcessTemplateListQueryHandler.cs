using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateList;

internal sealed class GetManufacturingProcessTemplateListQueryHandler
    : IRequestHandler<GetManufacturingProcessTemplateListQuery, PagedResult<ManufacturingProcessTemplateListItemDto>>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingProcessTemplateListQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ManufacturingProcessTemplateListItemDto>> Handle(
        GetManufacturingProcessTemplateListQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return new PagedResult<ManufacturingProcessTemplateListItemDto>([], 0, request.NormalizedPageNumber, request.NormalizedPageSize);

        var query = _db.ManufacturingProcessTemplates.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive);

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status.Value);

        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
            query = query.Where(x =>
                EF.Functions.ILike(x.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        query = (request.NormalizedSortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("externalid", false) => query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.VersionNo),
            ("externalid", true) => query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.VersionNo),
            ("name", false) => query.OrderBy(x => x.Name).ThenByDescending(x => x.VersionNo),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenByDescending(x => x.VersionNo),
            ("versionno", false) => query.OrderBy(x => x.VersionNo).ThenBy(x => x.ExternalId),
            ("versionno", true) => query.OrderByDescending(x => x.VersionNo).ThenBy(x => x.ExternalId),
            ("status", false) => query.OrderBy(x => x.Status).ThenBy(x => x.ExternalId),
            ("status", true) => query.OrderByDescending(x => x.Status).ThenBy(x => x.ExternalId),
            ("effectivefrom", false) => query.OrderBy(x => x.EffectiveFrom).ThenBy(x => x.ExternalId),
            ("effectivefrom", true) => query.OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.ExternalId),
            ("updateddate", false) => query.OrderBy(x => x.UpdatedDate ?? x.CreatedDate).ThenBy(x => x.ExternalId),
            _ => query.OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate).ThenBy(x => x.ExternalId)
        };

        var projected = query.Select(x => new ManufacturingProcessTemplateListItemDto
        {
            ManufacturingProcessTemplateId = x.ManufacturingProcessTemplateId,
            ExternalId = x.ExternalId,
            Name = x.Name,
            VersionNo = x.VersionNo,
            StageCount = x.Stages.Count,
            Status = x.Status,
            EffectiveFrom = x.EffectiveFrom,
            EffectiveTo = x.EffectiveTo,
            CreatedDate = x.CreatedDate,
            UpdatedDate = x.UpdatedDate
        });

        return await projected.ToPagedResultAsync(request.NormalizedPageNumber, request.NormalizedPageSize, cancellationToken);
    }
}
