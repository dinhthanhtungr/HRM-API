using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialSupplierPrice;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/material-suppliers")]
public sealed class MaterialSuppliersController : ControllerBase
{
    private readonly ISender _sender;

    public MaterialSuppliersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("{materialsSupplierId:guid}/price")]
    [Authorize(Policy = PlmPolicies.UpdateMaterialSupplierPrice)]
    public async Task<IActionResult> UpdatePrice(
        Guid materialsSupplierId,
        [FromBody] UpdateMaterialSupplierPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateMaterialSupplierPriceCommand(materialsSupplierId, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
