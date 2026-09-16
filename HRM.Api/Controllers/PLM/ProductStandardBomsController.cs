using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.AssignProductStandardBom;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetProductStandardBoms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/products/{productId:guid}/standard-bom")]
public sealed class ProductStandardBomsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductStandardBomsController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetCurrent(
        Guid productId,
        [FromQuery] DateTime? at,
        CancellationToken cancellationToken)
    {
        var rows = await _sender.Send(new GetProductStandardBomsQuery(productId, false, at), cancellationToken);
        return rows.Count == 0 ? NotFound() : Ok(rows[0]);
    }

    [HttpGet("history")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetHistory(Guid productId, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetProductStandardBomsQuery(productId, true), cancellationToken));

    [HttpPut]
    [Authorize(Policy = PlmPolicies.AssignStandardBom)]
    public async Task<IActionResult> Assign(
        Guid productId,
        [FromBody] AssignProductStandardBomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AssignProductStandardBomCommand(productId, request), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
