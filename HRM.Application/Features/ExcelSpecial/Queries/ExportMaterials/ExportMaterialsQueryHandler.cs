using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;

internal sealed class ExportMaterialsQueryHandler(
    IPLMReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    IMaterialsExcelRenderer renderer)
    : IRequestHandler<ExportMaterialsQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(ExportMaterialsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            !(permissions.HasPermission(ApplicationPermissions.Purchasing.ManageCatalog) ||
              permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders)))
            throw new UnauthorizedAccessException("Không có quyền đọc danh mục NVL hoặc chưa có công ty hợp lệ.");

        var rows = await (
            from material in db.Materials.AsNoTracking()
            join category in db.Categories.AsNoTracking() on material.CategoryId equals category.CategoryId
            where material.CompanyId == companyId && material.IsActive == true
                && category.CompanyId == companyId && category.IsActive == true && category.Types == "NVL"
            orderby material.ExternalId, material.Name, material.MaterialId
            select new MaterialExcelRow(material.ExternalId, material.Name, category.ExternalId, category.Name, material.CustomCode)
        ).ToListAsync(ct);

        ct.ThrowIfCancellationRequested();
        return OperationResult<byte[]>.Ok(renderer.Render(rows));
    }
}
