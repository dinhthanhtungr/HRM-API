using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.GetProcessingOptions;
internal sealed class GetProcessingOptionsQueryHandler(ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetProcessingOptionsQuery, ProcessingOrderOptions?>
{
    public Task<ProcessingOrderOptions?> Handle(GetProcessingOptionsQuery query, CancellationToken ct)
        => Task.FromResult<ProcessingOrderOptions?>(user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty &&
            permissions.HasPermission(ApplicationPermissions.PLM.ViewMaterialProcessing)
            ? new(Enum.GetValues<ManufacturingProductOrder>().Where(x => x != ManufacturingProductOrder.Unknown).Select(x => x.ToString()).ToList(),
                new[] { new ProcessingStepOption((int)StepOfProduct.BTS_GH, "B"), new ProcessingStepOption((int)StepOfProduct.BN_GH, "N"), new ProcessingStepOption((int)StepOfProduct.CT_GH, "L") })
            : null);
}
