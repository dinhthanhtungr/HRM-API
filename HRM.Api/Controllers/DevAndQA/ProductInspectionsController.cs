using HRM.Application.Features.DevAndQA.ProductInspections.Commands.CreateProductInspection;
using HRM.Application.Features.DevAndQA.ProductInspections.Commands.PatchProductInspection;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using HRM.Application.Features.DevAndQA.ProductInspections.Queries.ExportProductInspectionPdf;
using HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductCoaSources;
using HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductInspectionById;
using HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductInspections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/product-inspections")]
public sealed class ProductInspectionsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductInspectionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("coa-sources")]
    public async Task<IActionResult> GetCoaSources(
        [FromQuery] GetProductCoaSourcesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetProductInspections(
        [FromQuery] GetProductInspectionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetProductInspectionByIdQuery(id), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ProductInspectionWriteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateProductInspectionCommand(request), cancellationToken);
        return result.Success && result.Data is not null
            ? CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result)
            : BadRequest(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] ProductInspectionWriteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PatchProductInspectionCommand(id, request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(
        Guid id,
        [FromQuery] bool templateOnly,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ExportProductInspectionPdfQuery(id, templateOnly),
            cancellationToken);
        if (!result.Success || result.Data is null || result.Data.Content.Length == 0)
            return NotFound(result);

        return File(result.Data.Content, "application/pdf", result.Data.FileName);
    }
}
