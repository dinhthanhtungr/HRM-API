using HRM.Application.Features.PLM.Materials.TdsExtraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/materials/tds-extraction")]
public sealed class MaterialTdsExtractionController(TdsExtractionAccess access, ITdsJobQueue queue,
    IOptions<TdsExtractionOptions> options) : ControllerBase
{
    /// <summary>Đọc thư mục cấu hình; không import attachment hoặc cập nhật NVL.</summary>
    [HttpPost("jobs/source")]
    public IActionResult StartSource([FromQuery] int offset = 0, [FromQuery] int? limit = null) => Start(null, offset, limit);

    /// <summary>Nhận nhiều file, đọc bằng Gemini và tạo Excel để kiểm tra.</summary>
    [HttpPost("jobs/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(45 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 45 * 1024 * 1024)]
    public async Task<IActionResult> StartUpload([FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (!access.CanExtract) return Forbid();
        var settings = options.Value;
        if (!settings.Enabled) return StatusCode(503, new { errorCode = "extraction_disabled" });
        if (files.Count == 0 || files.Count > settings.MaxFiles)
            return BadRequest(new { errorCode = "batch_file_limit" });
        if (files.Sum(x => x.Length) > settings.MaxBatchBytes)
            return BadRequest(new { errorCode = "batch_size_limit" });
        var uploads = new List<TdsInputFile>();
        long total = 0;
        try
        {
            foreach (var file in files)
            {
                TdsExtractionRules.ValidateFile(file.FileName, file.Length, settings);
                await using var stream = file.OpenReadStream();
                var bytes = await TdsFileReader.ReadBoundedAsync(stream, settings.MaxFileBytes, cancellationToken);
                total += bytes.Length;
                if (total > settings.MaxBatchBytes) throw new TdsProcessingException("batch_size_limit");
                uploads.Add(new(file.FileName, bytes));
            }
        }
        catch (TdsProcessingException exception) { return BadRequest(new { errorCode = exception.Code }); }
        return Start(uploads);
    }

    [HttpGet("jobs/{jobId:guid}")]
    public IActionResult GetJob(Guid jobId)
    {
        if (!access.CanExtract) return Forbid();
        var job = access.Get(queue, jobId);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("jobs/{jobId:guid}/excel")]
    public IActionResult DownloadExcel(Guid jobId)
    {
        if (!access.CanExtract) return Forbid();
        var job = access.Get(queue, jobId);
        if (job is null) return NotFound();
        var bytes = access.GetExcel(queue, jobId);
        if (bytes is null) return Conflict(new { job.Status, job.ErrorCode });
        Response.Headers.CacheControl = "no-store";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TDS-{jobId:N}.xlsx");
    }

    /// <summary>Giải phóng job/result tạm đã hoàn tất; không xóa file nguồn.</summary>
    [HttpDelete("jobs/{jobId:guid}")]
    public IActionResult RemoveJob(Guid jobId)
    {
        if (!access.CanExtract) return Forbid();
        var job = access.Get(queue, jobId);
        if (job is null) return NotFound();
        return access.Remove(queue, jobId) ? NoContent() : Conflict(new { job.Status });
    }

    private IActionResult Start(IReadOnlyList<TdsInputFile>? files, int sourceOffset = 0, int? sourceLimit = null)
    {
        if (!access.CanExtract) return Forbid();
        var result = access.Start(queue, files, sourceOffset, sourceLimit);
        if (result.Accepted) return AcceptedAtAction(nameof(GetJob), new { jobId = result.Job!.JobId }, result.Job);
        return result.ErrorCode switch
        {
            "extraction_disabled" => StatusCode(503, new { result.ErrorCode }),
            "source_not_authorized" => Forbid(),
            "company_job_active" => Conflict(new { result.ErrorCode }),
            "job_capacity_reached" => StatusCode(429, new { result.ErrorCode }),
            _ => BadRequest(new { result.ErrorCode })
        };
    }
}
