// Tạm dừng toàn bộ API LabTest theo yêu cầu; mở lại sau khi đồng bộ schema và feature.
#if false
using System.Text.Json;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.LabTests.Commands.CreateLabTest;
using HRM.Application.Features.DevAndQA.LabTests.Commands.PatchLabTest;
using HRM.Application.Features.DevAndQA.LabTests.Commands.DeleteLabTest;
using HRM.Application.Features.DevAndQA.LabTests.Commands.PreviewLabTestImport;
using HRM.Application.Features.DevAndQA.LabTests.Commands.ImportLabTests;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTest;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetMaterialTestGrid;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

/// <summary>Bảng cơ lý IQC/LabTest, CRUD phiếu test độc lập và import có preview.</summary>
[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/lab-tests")]
public sealed class LabTestsController(ISender sender) : EvaluationControllerBase(sender)
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] GetMaterialTestGridQuery query, CancellationToken ct)
        => Execute(query with { Source = MaterialTestSource.LabTest }, ct);

    [HttpGet("grid")]
    public Task<IActionResult> Grid([FromQuery] GetMaterialTestGridQuery query, CancellationToken ct) => Execute(query, ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => Execute(new GetLabTestQuery(id), ct);

    [HttpPost]
    public Task<IActionResult> Create(LabTestWriteRequest request, CancellationToken ct)
        => Execute(new CreateLabTestCommand(request), ct, true);

    [HttpPatch("{id:guid}")]
    public Task<IActionResult> Patch(Guid id, LabTestWriteRequest request, CancellationToken ct)
        => Execute(new PatchLabTestCommand(id, request), ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => Execute(new DeleteLabTestCommand(id), ct);

    [HttpPost("import/preview")]
    [RequestSizeLimit(5_100_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_100_000)]
    public async Task<IActionResult> Preview([FromForm] LabTestImportForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length is <= 0 or > 5_000_000 || form.ColumnStandards?.Length > 20000)
            return BadRequest(OperationResult.Fail("File tối đa 5 MB; mapping tối đa 20000 ký tự."));
        var extension = Path.GetExtension(form.File.FileName).ToLowerInvariant();
        var mime = form.File.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx") || extension == ".csv" && mime is not ("text/csv" or "application/csv" or "text/plain" or "application/vnd.ms-excel" or "application/octet-stream") ||
            extension == ".xlsx" && mime is not ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or "application/octet-stream"))
            return BadRequest(OperationResult.Fail("Chỉ nhận CSV/XLSX với Content-Type phù hợp."));
        Dictionary<string, Guid>? mapping;
        try { mapping = string.IsNullOrWhiteSpace(form.ColumnStandards) ? null : JsonSerializer.Deserialize<Dictionary<string, Guid>>(form.ColumnStandards); }
        catch (JsonException) { return BadRequest(OperationResult.Fail("columnStandards phải là JSON tên cột → GUID chỉ tiêu.")); }
        using var buffer = new MemoryStream();
        await form.File.CopyToAsync(buffer, ct);
        return await Execute(new PreviewLabTestImportCommand(buffer.ToArray(), form.File.FileName, mapping), ct);
    }

    [HttpPost("import")]
    [RequestSizeLimit(10_000_000)]
    public Task<IActionResult> Import(ImportLabTestsCommand request, CancellationToken ct) => Execute(request, ct);
}

public sealed class LabTestImportForm
{
    public IFormFile? File { get; set; }
    public string? ColumnStandards { get; set; }
}

#endif
