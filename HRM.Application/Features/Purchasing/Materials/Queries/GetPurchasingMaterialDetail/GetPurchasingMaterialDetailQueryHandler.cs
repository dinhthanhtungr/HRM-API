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
        return detail;
    }
}
