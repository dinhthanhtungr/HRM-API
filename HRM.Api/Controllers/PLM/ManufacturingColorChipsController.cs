using HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingColorChip;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingColorChip;
using HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingColorChips;
using HRM.Application.Features.PLM.Manufacturing.Queries.PrintManufacturingColorChip;

using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[Authorize]
[ApiController]
[Route("api/v1/plm/manufacturing-color-chips")]
public sealed class ManufacturingColorChipsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListManufacturingColorChipsQuery request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetManufacturingColorChipQuery(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ManufacturingColorChipInput request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveManufacturingColorChipCommand(null, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ManufacturingColorChipInput request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveManufacturingColorChipCommand(id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new PrintManufacturingColorChipQuery(id), ct);
        return result.Success && result.Data is not null ? File(result.Data, "application/pdf", $"ColorChip_{id:N}.pdf") : BadRequest(result);
    }
}
