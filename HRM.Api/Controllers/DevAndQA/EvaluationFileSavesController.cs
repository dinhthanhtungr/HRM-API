using System.Text.Json;
using System.Data.Common;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Controllers.DevAndQA;

/// <summary>Một lần Lưu cho payload và file; route xác định cha, multipart không nhận collection ID.</summary>
[Authorize]
[ApiController]
[Route("api/v1/dev-and-qa")]
[RequestSizeLimit(55_000_000)]
[RequestFormLimits(MultipartBodyLengthLimit = 55_000_000)]
public sealed class EvaluationFileSavesController(IEvaluationFileSaveService saves) : ControllerBase
{
    [HttpPost("lab-tests/save-with-attachments")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> CreateLab(CancellationToken ct) => Save(EvaluationFileResource.Lab, null, true, ct);

    [HttpPatch("lab-tests/{id:guid}/save-with-attachments")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> PatchLab(Guid id, CancellationToken ct) => Save(EvaluationFileResource.Lab, id.ToString(), false, ct);
    [HttpPost("iqc/save-with-attachments")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> CreateIqc(CancellationToken ct) => Save(EvaluationFileResource.Iqc, null, true, ct);
    [HttpPatch("iqc/{voucherDetailId:long}/save-with-attachments")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> PatchIqc(long voucherDetailId, CancellationToken ct) => Save(EvaluationFileResource.Iqc, voucherDetailId.ToString(), false, ct);
    [HttpPost("coa-certificates/save-with-attachments")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> CreateCoa(CancellationToken ct) => Save(EvaluationFileResource.Coa, null, true, ct);
    [HttpPatch("coa-certificates/{id:guid}/save-with-attachments")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> PatchCoa(Guid id, CancellationToken ct) => Save(EvaluationFileResource.Coa, id.ToString(), false, ct);
    [HttpGet("evaluation-attachments/{resource}/{parentId:guid}")]
    public Task<IActionResult> List(EvaluationFileResource resource, Guid parentId, CancellationToken ct) =>
        Execute(async () => Ok(OperationResult<IReadOnlyList<EvaluationFileGroupDto>>.Ok(await saves.ListAsync(resource, parentId, ct))));

    [HttpGet("evaluation-attachments/{resource}/{parentId:guid}/{ownerId:guid}/{attachmentId:guid}/content")]
    [HttpGet("evaluation-attachments/{resource}/{parentId:guid}/{ownerId:guid}/{attachmentId:guid}/download")]
    public Task<IActionResult> Content(EvaluationFileResource resource, Guid parentId, Guid ownerId, Guid attachmentId, CancellationToken ct) =>
        Execute(async () =>
        {
            var content = await saves.ContentAsync(resource, parentId, ownerId, attachmentId, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = "private, no-store";
            if (!Request.Path.Value!.EndsWith("/download", StringComparison.Ordinal) &&
                (content.ContentType == "application/pdf" || content.ContentType.StartsWith("image/", StringComparison.Ordinal)))
                return File(content.Stream, content.ContentType, enableRangeProcessing: true);
            return File(content.Stream, content.ContentType, content.FileName, enableRangeProcessing: true);
        });

    private Task<IActionResult> Save(EvaluationFileResource resource, string? key, bool create, CancellationToken ct) =>
        Execute(async () =>
        {
            if (!Request.HasFormContentType) return BadRequest(OperationResult.Fail("Cần multipart/form-data."));
            var form = await Request.ReadFormAsync(ct);
            var payload = form["payload"];
            var manifest = form["attachments"];
            if (payload.Count != 1 || payload[0]!.Length > 1_000_000 || manifest.Count > 1 || manifest.ToString().Length > 100_000 ||
                form.Files.Count > 200 || form.Files.Sum(x => x.Length) > 50_000_000)
                return BadRequest(OperationResult.Fail("Payload hoặc file vượt giới hạn request."));
            using var json = JsonDocument.Parse(payload[0]!);
            var bindings = manifest.Count == 0 ? [] : JsonSerializer.Deserialize<List<EvaluationFileBinding>>(manifest[0]!, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new EvaluationRequestException("attachments không được null.");
            var streams = new List<EvaluationNamedUpload>();
            try
            {
                foreach (var file in form.Files)
                    streams.Add(new(file.Name, new AttachmentUploadFile(file.OpenReadStream(), file.FileName, file.ContentType, file.Length)));
                var result = await saves.SaveAsync(resource, key, create, new(json.RootElement, bindings), streams, ct);
                return StatusCode(create ? 201 : 200, OperationResult<EvaluationFileSaveDto>.Ok(result));
            }
            finally { foreach (var file in streams) await file.File.Stream.DisposeAsync(); }
        });

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (EvaluationRequestException error) { return StatusCode(error.StatusCode, OperationResult.Fail(error.Message)); }
        catch (JsonException) { return BadRequest(OperationResult.Fail("JSON multipart không hợp lệ.")); }
        catch (InvalidDataException) { return BadRequest(OperationResult.Fail("Multipart vượt giới hạn hoặc không hợp lệ.")); }
        catch (IOException) { return StatusCode(503, OperationResult.Fail("Không lưu/đọc được file. Hãy thử lại.")); }
        catch (DbUpdateException) { return Conflict(OperationResult.Fail("Dữ liệu thay đổi hoặc liên kết không hợp lệ. Hãy tải lại.")); }
        catch (DbException) { return Conflict(OperationResult.Fail("Không hoàn tất được transaction. Hãy tải lại để kiểm tra trạng thái lưu.")); }
    }
}
