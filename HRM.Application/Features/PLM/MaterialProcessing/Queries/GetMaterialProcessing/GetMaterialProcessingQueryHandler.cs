using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.GetMaterialProcessing;
internal sealed class GetMaterialProcessingQueryHandler(IMaterialProcessingDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions, ProcessingMaterialResolver resolver)
    : IRequestHandler<GetMaterialProcessingQuery, MaterialProcessingDto?>
{
    public async Task<MaterialProcessingDto?> Handle(GetMaterialProcessingQuery query, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewMaterialProcessing) || user.CompanyId is null) return null;
        var order = await SemiFinishedProcessing.Orders(db, user.CompanyId.Value).AsNoTracking().Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.MfgProductionOrderId == query.Id, ct);
        if (order is null) return null;
        var formula = await SemiFinishedProcessing.FormulaAsync(db, user.CompanyId.Value, query.Id, ct);
        if (formula is null) return null;
        var row = SemiFinishedProcessing.Adapt(order, formula);
        return MaterialProcessingProjection.ToDto(row, await resolver.BaseAsync(row.Material, user.CompanyId.Value, ct));
    }
}
