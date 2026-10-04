using HRM.Application.Abstractions.Persistence.MRO;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.MRO.Equipment.Dtos;
using HRM.Domain.Entities.MROSchema;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Features.MRO.Equipment.Services;

/// <summary>Company-scoped machine catalog and technical specifications; every operation enforces its capability.</summary>
internal sealed partial class EquipmentManagementService(
    IEquipmentDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions) : IEquipmentManagementService
{
    private bool Allowed(string capability) => user.IsAuthenticated && user.CompanyId is { } companyId
        && companyId != Guid.Empty && permissions.HasPermission(capability);

    public EquipmentCapabilities GetCapabilities() => new(
        Allowed(ApplicationPermissions.Equipment.View), Allowed(ApplicationPermissions.Equipment.Create),
        Allowed(ApplicationPermissions.Equipment.Update), Allowed(ApplicationPermissions.Equipment.Delete));

    private IQueryable<EquipmentMRO> ScopedMachines() => EquipmentScope.ForCompany(db.EquipmentsMro, user.CompanyId);

    public async Task<EquipmentResult<PagedResult<EquipmentListItem>>> GetListAsync(EquipmentListRequest request, CancellationToken ct)
    {
        if (!Allowed(ApplicationPermissions.Equipment.View))
            return EquipmentResult<PagedResult<EquipmentListItem>>.Fail(403, "forbidden");
        if (request.Page < 1 || request.PageSize is < 1 or > 100
            || (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            return EquipmentResult<PagedResult<EquipmentListItem>>.Fail(400, "invalidPagination");
        var query = ScopedMachines().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(request.Keyword.Trim());
            query = query.Where(x => EF.Functions.ILike(x.EquipmentExternalId, pattern, PostgresSearchPattern.EscapeCharacter)
                || EF.Functions.ILike(x.EquipmentName, pattern, PostgresSearchPattern.EscapeCharacter));
        }
        if (!string.IsNullOrWhiteSpace(request.GroupType)) query = query.Where(x => x.GroupType == request.GroupType.Trim());
        if (request.AreaId.HasValue) query = query.Where(x => x.AreaId == request.AreaId.Value);
        var result = await query.OrderBy(x => x.EquipmentExternalId).ThenBy(x => x.EquipmentId)
            .Select(x => new EquipmentListItem(x.EquipmentId, x.EquipmentExternalId, x.EquipmentName,
                x.GroupType, x.AreaId, x.AreaExternalId, x.PartId, x.PartExternalId))
            .ToPagedResultAsync(request.Page, request.PageSize, ct);
        return new(result);
    }

    public async Task<EquipmentResult<EquipmentDetailDto>> GetDetailAsync(int id, CancellationToken ct)
    {
        if (!Allowed(ApplicationPermissions.Equipment.View)) return EquipmentResult<EquipmentDetailDto>.Fail(403, "forbidden");
        var machine = await ScopedMachines().AsNoTracking().Where(x => x.EquipmentId == id)
            .Select(x => new EquipmentListItem(x.EquipmentId, x.EquipmentExternalId, x.EquipmentName,
                x.GroupType, x.AreaId, x.AreaExternalId, x.PartId, x.PartExternalId)).SingleOrDefaultAsync(ct);
        if (machine is null) return EquipmentResult<EquipmentDetailDto>.Fail(404, "notFound");
        var details = await db.EquipmentDetailsMro.AsNoTracking()
            .Where(x => x.EquipmentId == id && x.Equipment.FactoryId == user.CompanyId)
            .Select(x => new EquipmentDetailsRequest { SerialNo = x.SerialNo, Manufacturer = x.Manufacturer,
                Model = x.Model, PurchaseDate = x.PurchaseDate, CommissioningDate = x.CommissioningDate,
                WarrantyUntil = x.WarrantyUntil, Notes = x.Notes, EquipmentTypeId = x.EquipmentTypeId })
            .SingleOrDefaultAsync(ct) ?? new EquipmentDetailsRequest();
        return new(new(machine, details, await ReadSpecsAsync(id, ct)));
    }

    public async Task<EquipmentResult<EquipmentOptions>> GetOptionsAsync(CancellationToken ct)
    {
        if (!Allowed(ApplicationPermissions.Equipment.View)) return EquipmentResult<EquipmentOptions>.Fail(403, "forbidden");
        // Areas, parts and equipment types are shared catalogs without CompanyId in the existing schema.
        var areas = await db.AreasMro.AsNoTracking().OrderBy(x => x.AreaName)
            .Select(x => new EquipmentAreaOption(x.AreaId, x.AreaExternalId, x.AreaName)).ToListAsync(ct);
        var parts = await db.Parts.AsNoTracking().OrderBy(x => x.PartName)
            .Select(x => new EquipmentPartOption(x.PartId, x.ExternalId, x.PartName)).ToListAsync(ct);
        var types = await db.EquipmentTypesMro.AsNoTracking().OrderBy(x => x.EquipmentTypeName)
            .Select(x => new EquipmentTypeOption(x.EquipmentTypeId, x.EquipmentTypeName)).ToListAsync(ct);
        var groups = await ScopedMachines().AsNoTracking().Where(x => x.GroupType != null && x.GroupType != "")
            .Select(x => x.GroupType!).Distinct().OrderBy(x => x).ToListAsync(ct);
        return new(new(areas, parts, types, groups));
    }

    private async Task<string?> PersistAsync(CancellationToken ct, Func<Task>? operation = null)
    {
        try
        {
            if (operation is null) await db.SaveChangesAsync(ct);
            else await operation();
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { return "duplicateCode"; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" })
        { return "referencedOrInvalidReference"; }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
