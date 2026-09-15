using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.PrintLabels.Commands.CreatePrintLabelCatalog;
using HRM.Application.Features.PLM.PrintLabels.Queries.GetTemplateSelection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/print-label-templates")]
public sealed class PrintLabelTemplatesController : ControllerBase
{
    private readonly ISender _sender;
    public PrintLabelTemplatesController(ISender sender) => _sender = sender;

    [HttpGet("{printLabelTemplateId:guid}/selection")]
    public async Task<IActionResult> GetSelection(Guid printLabelTemplateId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetTemplateSelectionQuery(printLabelTemplateId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PlmPolicies.ManageCustomerLabels)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreatePrintLabelTemplateCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(GetSelection), new { printLabelTemplateId = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("logos")]
    [Authorize(Policy = PlmPolicies.ManageCustomerLabels)]
    public async Task<IActionResult> CreateLogo([FromBody] CreatePrintLabelLogoCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
