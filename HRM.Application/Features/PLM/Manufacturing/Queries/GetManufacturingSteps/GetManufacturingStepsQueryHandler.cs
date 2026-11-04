using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingSteps;

internal sealed class GetManufacturingStepsQueryHandler(ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetManufacturingStepsQuery, OperationResult<IReadOnlyList<ManufacturingStepOption>>>
{
    public Task<OperationResult<IReadOnlyList<ManufacturingStepOption>>> Handle(GetManufacturingStepsQuery request, CancellationToken ct)
        => Task.FromResult(user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty
            && (permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders)
                || permissions.HasPermission(ApplicationPermissions.PLM.CreateProductionOrders))
            ? OperationResult<IReadOnlyList<ManufacturingStepOption>>.Ok(ManufacturingEditorPolicy.Steps)
            : OperationResult<IReadOnlyList<ManufacturingStepOption>>.Fail("Không có quyền đọc công đoạn."));
}
