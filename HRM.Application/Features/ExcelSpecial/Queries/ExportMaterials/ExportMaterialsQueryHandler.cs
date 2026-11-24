using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;

internal sealed class ExportMaterialsQueryHandler(
    IPLMReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    IMaterialsExcelRenderer renderer, IPurchaseOrderDbContext purchasingDb, IDateTimeProvider clock)
    : IRequestHandler<ExportMaterialsQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(ExportMaterialsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            !(permissions.HasPermission(ApplicationPermissions.Purchasing.ManageCatalog) ||
              permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders)))
            throw new UnauthorizedAccessException("Không có quyền đọc danh mục NVL hoặc chưa có công ty hợp lệ.");

        var year = TimeZoneInfo.ConvertTime(clock.Now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok")).Year;
        var yearStart = new DateTime(year, 1, 1);
        var nextYearStart = yearStart.AddYears(1);

        var formulaItems = db.FormulaMaterials.AsNoTracking()
            .Where(item => item.IsActive && item.Formula.IsActive && item.Formula.CompanyId == companyId
                && item.Formula.CreatedDate >= yearStart && item.Formula.CreatedDate < nextYearStart);
        var manufacturingItems = db.ManufacturingFormulaMaterials.AsNoTracking()
            .Where(item => item.IsActive && item.ManufacturingFormula.IsActive
                && item.ManufacturingFormula.CompanyId == companyId
                && item.ManufacturingFormula.CreatedDate >= yearStart && item.ManufacturingFormula.CreatedDate < nextYearStart);
        var purchaseItems = purchasingDb.PurchaseOrderDetails.AsNoTracking()
            .Where(item => item.IsActive && item.PurchaseOrder.IsActive != false && item.PurchaseOrder.CompanyId == companyId
                && item.PurchaseOrder.CreateDate >= yearStart && item.PurchaseOrder.CreateDate < nextYearStart
                && (item.PurchaseOrder.Status == PurchaseOrderStatus.InProgress
                    || item.PurchaseOrder.Status == PurchaseOrderStatus.Completed
                    || item.PurchaseOrder.Status == PurchaseOrderStatus.ExchangeRequested));

        var rows = await (
            from material in db.Materials.AsNoTracking()
            join category in db.Categories.AsNoTracking() on material.CategoryId equals category.CategoryId
            where material.CompanyId == companyId && material.IsActive == true
                && category.CompanyId == companyId && category.IsActive == true && category.Types == "NVL"
            orderby material.ExternalId, material.Name, material.MaterialId
            select new MaterialExcelRow(material.ExternalId, material.Name, category.ExternalId, category.Name, material.CustomCode,
                formulaItems.Any(item => item.MaterialId == material.MaterialId)
                || manufacturingItems.Any(item => item.MaterialId == material.MaterialId)
                || purchaseItems.Any(item => item.MaterialId == material.MaterialId))
        ).ToListAsync(ct);

        ct.ThrowIfCancellationRequested();
        return OperationResult<byte[]>.Ok(renderer.Render(rows));
    }
}
