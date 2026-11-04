using HRM.Application.Features.DevAndQA.MeasurementMethods.Queries.GetMeasurementMethods;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Queries.GetMeasurementMethod;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.CreateMeasurementMethod;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.PatchMeasurementMethod;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/measurement-methods")]
public sealed class MeasurementMethodsController(ISender sender) : EvaluationControllerBase(sender)
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] GetMeasurementMethodsQuery query, CancellationToken ct) => Execute(query, ct);
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => Execute(new GetMeasurementMethodQuery(id), ct);
    [HttpPost]
    public Task<IActionResult> Create(CreateMeasurementMethodCommand request, CancellationToken ct) => Execute(request, ct, true);
    [HttpPatch("{id:guid}")]
    public Task<IActionResult> Patch(Guid id, PatchMeasurementMethodCommand request, CancellationToken ct) => Execute(request with { Id = id }, ct);
}
