using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaSource;

internal sealed class GetManufacturingFormulaSourceQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility,
    ManufacturingFormulaReadService reader
) : IRequestHandler<GetManufacturingFormulaSourceQuery, OperationResult<ProductionOrderFormulaSourceDto>>
{
    public async Task<OperationResult<ProductionOrderFormulaSourceDto>> Handle(GetManufacturingFormulaSourceQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Không có quyền xem vật tư công thức.");
        }

        var company = user.CompanyId!.Value;
        var formula = await db.ManufacturingFormulas.AsNoTracking().Where(x => x.ManufacturingFormulaId == request.Id && x.CompanyId == company && x.IsActive)
            .Select(x => new { x.ExternalId, x.Note })
            .FirstOrDefaultAsync(ct);
        if (formula is null)
        {
            return Fail("Không tìm thấy VA active trong công ty.");
        } // Legacy material-view uses saved costs; prefill and Lab View have distinct pricing rules.

        var batches = await reader.ReadBatchAsync(company, [request.Id], [], false, false, true, ct, refreshPrices: false);
        return OperationResult<ProductionOrderFormulaSourceDto>.Ok(new ProductionOrderFormulaSourceDto(
            request.Id,
            formula.ExternalId,
            formula.Note,
            visibility.CanViewFormulaPrices(),
            batches.GetValueOrDefault((request.Id, false)) ?? []
        ));
    }

    private static OperationResult<ProductionOrderFormulaSourceDto> Fail(string message) => OperationResult<ProductionOrderFormulaSourceDto>.Fail(message);
}
