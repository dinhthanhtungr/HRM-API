using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.Materials.Dtos;
using HRM.Application.Features.Purchasing.Materials.Services;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterialDetail;

internal sealed class GetPurchasingMaterialDetailQueryHandler(
    IPurchaseOrderDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPurchasingMaterialDetailQuery, PurchasingMaterialDetailDto?>
{
    public async Task<PurchasingMaterialDetailDto?> Handle(GetPurchasingMaterialDetailQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !(PurchaseOrderAccess.CanManage(permissions) || permissions.HasPermission(ApplicationPermissions.Purchasing.ManageCatalog)) || user.CompanyId is not { } companyId || companyId == Guid.Empty)
            throw new UnauthorizedAccessException("Không có quyền xem danh mục vật tư mua hàng.");
        if (request.MaterialId == Guid.Empty) return null;
        var detail = await db.Materials.AsNoTracking()
            .Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive == true) && x.MaterialId == request.MaterialId)
            .Select(PurchasingMaterialReadModel.Detail(companyId, PurchaseOrderAccess.CanManage(permissions))).FirstOrDefaultAsync(ct);
        if (detail is not null) PurchasingMaterialReadModel.NormalizeAvailability(detail);
        if (detail is null)
            detail = await db.Products.AsNoTracking().Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive)
                    && x.ProductId == request.MaterialId && x.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive && s.RequestType == "SemiFinished"))
                .Select(x => new PurchasingMaterialDetailDto { MaterialId = x.ProductId, ExternalId = x.ColourCode, CustomCode = x.Code,
                    Name = x.Name, CategoryId = x.CategoryId, CategoryName = x.Category != null && x.Category.CompanyId == companyId ? x.Category.Name : null,
                    IsActive = x.IsActive, IsSemiFinished = true, Unit = x.Unit, Weight = x.Weight,
                    CreatedDate = x.CreatedDate, UpdatedDate = x.UpdatedDate, CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy,
                    Package = x.SampleRequests.Where(s => s.CompanyId == companyId && s.IsActive && s.RequestType == "SemiFinished")
                        .OrderByDescending(s => s.CreatedDate).Select(s => s.Package).FirstOrDefault() }).FirstOrDefaultAsync(ct);
        if (detail?.IsSemiFinished == true)
            detail.SourceMaterials = await db.Products.AsNoTracking()
                .Where(x => x.ProductId == request.MaterialId && x.CompanyId == companyId && (request.IncludeInactive || x.IsActive))
                .SelectMany(x => x.Formulas.Where(f => f.CompanyId == companyId && f.IsActive && f.IsSelect)
                    .OrderByDescending(f => f.UpdatedDate ?? f.CreatedDate).ThenByDescending(f => f.FormulaId).Take(1))
                .SelectMany(f => f.FormulaMaterials.Where(l => l.IsActive && l.MaterialId != null
                    && l.Material.CompanyId == companyId && l.Material.IsActive == true)
                    .OrderBy(l => l.LineNo).Select(l => new PurchasingMaterialSourceDto {
                        MaterialId = l.MaterialId!.Value, ExternalId = l.Material.ExternalId,
                        Name = l.Material.Name, Unit = l.Material.Unit, Std = l.Quantity, FormulaCode = f.ExternalId }))
                .ToListAsync(ct);
        return detail;
    }
}
