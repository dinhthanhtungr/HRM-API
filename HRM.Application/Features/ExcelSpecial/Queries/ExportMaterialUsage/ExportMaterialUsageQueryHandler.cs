using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMaterialUsage;

internal sealed class ExportMaterialUsageQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IPLMFieldVisibilityService visibility,
    IMaterialUsageExcelRenderer renderer)
    : IRequestHandler<ExportMaterialUsageQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(ExportMaterialUsageQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !user.IsInRole(ApplicationRoles.Developer) ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders) || !visibility.CanViewFormulaMaterials())
            throw new UnauthorizedAccessException();
        if (request.MaterialCodes is not { Length: > 0 and <= 500 } ||
            request.MaterialCodes.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100))
            return OperationResult<byte[]>.Fail("Nhập từ 1 đến 500 mã NVL, mỗi mã tối đa 100 ký tự.");
        if (request.From?.Date > request.To?.Date || request.To?.Date == DateTime.MaxValue.Date)
            return OperationResult<byte[]>.Fail("Khoảng ngày tạo VA không hợp lệ.");
        var codes = request.MaterialCodes.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
        var materials = await db.Materials.AsNoTracking()
            .Where(x => x.CompanyId == company && x.IsActive == true && x.ExternalId != null && codes.Contains(x.ExternalId.ToUpper()))
            .Select(x => new { x.MaterialId, Code = x.ExternalId! }).ToListAsync(ct);
        var missing = codes.Except(materials.Select(x => x.Code.ToUpperInvariant())).ToArray();
        if (missing.Length > 0)
            return OperationResult<byte[]>.Fail("Không tìm thấy NVL đang hoạt động: " + string.Join(", ", missing));
        var ids = materials.Select(x => x.MaterialId).ToArray();
        var matchingIds = db.ManufacturingFormulaMaterials.AsNoTracking()
            .Where(x => x.IsActive && x.MaterialId.HasValue && ids.Contains(x.MaterialId.Value) &&
                (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure))
            .Select(x => x.ManufacturingFormulaId);
        var allVa = (
            from va in db.ManufacturingFormulas.AsNoTracking()
            join selection in db.ProductionSelectVersions.AsNoTracking() on va.ManufacturingFormulaId equals selection.ManufacturingFormulaId
            join order in db.MfgProductionOrders.AsNoTracking() on selection.MfgProductionOrderId equals order.MfgProductionOrderId
            join product in db.Products.AsNoTracking() on order.ProductId equals product.ProductId
            where va.CompanyId == company && va.IsActive && EF.Functions.ILike(va.ExternalId, "VA%") &&
                selection.CompanyId == company && order.CompanyId == company && order.IsActive &&
                product.CompanyId == company && product.IsActive && product.ColourCode != null && product.ColourCode != ""
            select new { va.ManufacturingFormulaId, va.ExternalId, va.CreatedDate, product.ColourCode, product.Name }
        ).Distinct();
        var matchingVa = allVa.Where(x => matchingIds.Contains(x.ManufacturingFormulaId));
        if (request.From is { } from) matchingVa = matchingVa.Where(x => x.CreatedDate >= from.Date);
        if (request.To is { } to)
        {
            var exclusiveEnd = to.Date.AddDays(1);
            matchingVa = matchingVa.Where(x => x.CreatedDate < exclusiveEnd);
        }
        var candidates = await matchingVa
            .OrderBy(x => x.ColourCode).ThenByDescending(x => x.CreatedDate).Take(50_001).ToListAsync(ct);
        if (candidates.Count > 50_000)
            return OperationResult<byte[]>.Fail("Có quá nhiều VA phù hợp; hãy giảm danh sách NVL.");
        var selected = MaterialUsageSelection.Latest(candidates.Select(x => new MaterialUsageCandidate(x.ManufacturingFormulaId, x.ExternalId, x.CreatedDate, x.ColourCode, x.Name)));
        var selectedIds = selected.Select(x => x.ManufacturingFormulaId).ToArray();
        var used = await db.ManufacturingFormulaMaterials.AsNoTracking()
            .Where(x => x.IsActive && selectedIds.Contains(x.ManufacturingFormulaId) &&
                x.ManufacturingFormula.CompanyId == company && x.MaterialId.HasValue && ids.Contains(x.MaterialId.Value) &&
                (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure))
            .Select(x => new { x.ManufacturingFormulaId, MaterialId = x.MaterialId!.Value }).Distinct().ToListAsync(ct);
        var colours = selected.Select(x => x.ColourCode).Distinct().ToArray();
        var latest = await allVa.Where(x => colours.Contains(x.ColourCode))
            .GroupBy(x => x.ColourCode).Select(g => new { ColourCode = g.Key, Date = g.Max(x => x.CreatedDate) }).ToListAsync(ct);
        var materialMap = materials.ToDictionary(x => x.MaterialId, x => x.Code);
        var usedMap = used.GroupBy(x => x.ManufacturingFormulaId).ToDictionary(g => g.Key,
            g => string.Join(", ", g.Select(x => materialMap[x.MaterialId])
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
        if (usedMap.Values.Any(x => x.Length > 32_767))
            return OperationResult<byte[]>.Fail("Danh sách NVL của một VA vượt giới hạn ô Excel; hãy giảm danh sách NVL.");
        var latestMap = latest.GroupBy(x => x.ColourCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Date), StringComparer.OrdinalIgnoreCase);
        var rows = selected.Select(x => new MaterialUsageExcelRow(x.ColourCode, x.Name, x.ExternalId,
            usedMap.GetValueOrDefault(x.ManufacturingFormulaId, string.Empty), latestMap[x.ColourCode.Trim()]))
            .OrderBy(x => x.ColourCode, StringComparer.OrdinalIgnoreCase).ToList();
        return OperationResult<byte[]>.Ok(renderer.Render(rows));
    }
}