using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabel;
using HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabelDetail;
using HRM.Application.Features.PLM.CustomerLabels.Commands.PatchCustomerLabel;
using HRM.Application.Features.PLM.CustomerLabels.Commands.PatchCustomerLabelDetail;
using HRM.Application.Features.PLM.CustomerLabels.Queries.GetCustomerLabelById;
using HRM.Application.Features.PLM.CustomerLabels.Queries.GetCustomerLabels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

/// <summary>
/// Quản lý dữ liệu nhãn theo khách hàng. Response GET luôn gồm cả detail inactive.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/plm/customer-labels")]
public sealed class CustomerLabelsController : ControllerBase
{
    private readonly ISender _sender;

    public CustomerLabelsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetCustomerLabelsQuery query, CancellationToken cancellationToken)
        => Ok(await _sender.Send(query, cancellationToken));

    [HttpGet("{customerLabelHeaderId:guid}")]
    public async Task<IActionResult> GetById(Guid customerLabelHeaderId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerLabelByIdQuery(customerLabelHeaderId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PlmPolicies.EditProductTechnicalInfo)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerLabelCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { customerLabelHeaderId = result.Data!.CustomerLabelHeaderId }, result)
            : BadRequest(result);
    }

    [HttpPatch("{customerLabelHeaderId:guid}")]
    [Authorize(Policy = PlmPolicies.EditProductTechnicalInfo)]
    public async Task<IActionResult> Patch(
        Guid customerLabelHeaderId,
        [FromBody] PatchCustomerLabelCommand command,
        CancellationToken cancellationToken)
    {
        command.CustomerLabelHeaderId = customerLabelHeaderId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{customerLabelHeaderId:guid}/details")]
    [Authorize(Policy = PlmPolicies.EditProductTechnicalInfo)]
    public async Task<IActionResult> CreateDetail(
        Guid customerLabelHeaderId,
        [FromBody] CreateCustomerLabelDetailCommand command,
        CancellationToken cancellationToken)
    {
        command.CustomerLabelHeaderId = customerLabelHeaderId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{customerLabelHeaderId:guid}/details/{customerLabelDetailId:guid}")]
    [Authorize(Policy = PlmPolicies.EditProductTechnicalInfo)]
    public async Task<IActionResult> PatchDetail(
        Guid customerLabelHeaderId,
        Guid customerLabelDetailId,
        [FromBody] PatchCustomerLabelDetailCommand command,
        CancellationToken cancellationToken)
    {
        command.CustomerLabelHeaderId = customerLabelHeaderId;
        command.CustomerLabelDetailId = customerLabelDetailId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
