using HRM.Application.Features.PLM.MaterialProcessing.Commands.SaveMaterialProcessing;
using HRM.Application.Features.PLM.MaterialProcessing.Commands.ConfirmMaterialProcessing;
using HRM.Application.Features.PLM.MaterialProcessing.Queries.GetMaterialProcessing;
using HRM.Application.Features.PLM.MaterialProcessing.Queries.GetMaterialProcessings;
using HRM.Application.Features.PLM.MaterialProcessing.Queries.LookupProcessingMaterials;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using MediatR;
using HRM.Application.Features.PLM.MaterialProcessing.Commands.PatchMaterialProcessing;
using HRM.Application.Features.PLM.MaterialProcessing.Queries.GetProcessingOptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/material-processing-formulas")]
public sealed class MaterialProcessingController(ISender sender) : ControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct)
    {
        var result = await sender.Send(new GetProcessingOptionsQuery(), ct);
        return result is null ? Forbid() : Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetMaterialProcessingsQuery(pageNumber), ct);
        return result is null ? Forbid() : Ok(result);
    }

    [HttpGet("materials")]
    public async Task<IActionResult> Materials([FromQuery] string? keyword, [FromQuery] string? processingType, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var query = new LookupProcessingMaterialsQuery(keyword, processingType);
        var result = await sender.Send(query, ct);
        if (result is not null)
        {
            var total = $"mpf_total;dur={watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
            Response.Headers.Append("Server-Timing", string.IsNullOrEmpty(query.ServerTiming) ? total : query.ServerTiming + ", " + total);
        }
        return result is null ? Forbid() : Ok(result);
    }

    [HttpGet("materials/{id:guid}/stock")]
    public async Task<IActionResult> Stock(Guid id, [FromQuery] int itemType = 0, [FromQuery] Guid? orderId = null, CancellationToken ct = default)
    {
        var result = await sender.Send(new HRM.Application.Features.PLM.MaterialProcessing.Queries.ProcessingStockQuery(id, itemType, orderId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMaterialProcessingQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveMaterialProcessingRequest request, CancellationToken ct)
        => WriteResult(await sender.Send(new SaveMaterialProcessingCommand(null, request), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveMaterialProcessingRequest request, CancellationToken ct)
        => WriteResult(await sender.Send(new SaveMaterialProcessingCommand(id, request), ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, [FromBody] PatchMaterialProcessingRequest request, CancellationToken ct)
        => WriteResult(await sender.Send(new PatchMaterialProcessingCommand(id, request), ct));

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmMaterialProcessingRequest request, CancellationToken ct)
        => WriteResult(await sender.Send(new ConfirmMaterialProcessingCommand(id, request), ct));

    private IActionResult WriteResult(MaterialProcessingResult result) => result.Code switch
    {
        "Ok" => Ok(result.Data),
        "Forbidden" => Forbid(),
        "NotFound" => NotFound(),
        "Conflict" or "SourceChanged" or "OutputCodeExists" or "ImmutableIdentity" => Conflict(new { result.Code }),
        _ => BadRequest(new { result.Code, result.Message })
    };
}
