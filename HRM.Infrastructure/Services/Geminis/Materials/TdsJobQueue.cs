using System.Threading.Channels;
using HRM.Application.Features.PLM.Materials.TdsExtraction;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Services.Geminis.Materials;

/// <summary>Queue tạm có giới hạn; job/result hết hạn và mất khi restart, không lưu DB.</summary>
internal sealed class TdsJobQueue(IOptions<TdsExtractionOptions> options) : ITdsJobQueue
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Entry> _jobs = [];
    private readonly Channel<TdsWorkItem> _channel = Channel.CreateBounded<TdsWorkItem>(8);

    public TdsStartResult TryEnqueue(Guid companyId, Guid requestedBy, IReadOnlyList<TdsInputFile>? uploads,
        int sourceOffset = 0, int? sourceLimit = null)
    {
        var settings = options.Value;
        if (!settings.Enabled) return new(false, null, "extraction_disabled");
        if (companyId == Guid.Empty || requestedBy == Guid.Empty) return new(false, null, "forbidden");
        if (uploads is null && settings.SourceCompanyId != companyId) return new(false, null, "source_not_authorized");
        if (sourceOffset < 0 || sourceLimit is < 1 || sourceLimit > settings.MaxFiles)
            return new(false, null, "invalid_source_batch");
        if (uploads is not null)
        {
            if (uploads.Count == 0 || uploads.Count > settings.MaxFiles) return new(false, null, "batch_file_limit");
            if (uploads.Sum(x => x.Content.LongLength) > settings.MaxBatchBytes) return new(false, null, "batch_size_limit");
            foreach (var file in uploads)
            {
                try { TdsExtractionRules.ValidateFile(file.FileName, file.Content.LongLength, settings); }
                catch (TdsProcessingException error) { return new(false, null, error.Code); }
            }
        }
        lock (_sync)
        {
            Cleanup(settings);
            if (_jobs.Values.Any(x => x.CompanyId == companyId && x.Dto.Status is "queued" or "running"))
                return new(false, null, "company_job_active");
            if (_jobs.Count >= settings.MaxRetainedJobs) return new(false, null, "job_capacity_reached");
            var id = Guid.NewGuid();
            var entry = new Entry(companyId, requestedBy,
                new(id, "queued", uploads?.Count ?? 0, 0, null, DateTimeOffset.UtcNow, null));
            _jobs.Add(id, entry);
            if (!_channel.Writer.TryWrite(new(id, companyId, requestedBy, uploads, sourceOffset, sourceLimit)))
            {
                _jobs.Remove(id);
                return new(false, null, "job_capacity_reached");
            }
            return new(true, entry.Dto, null);
        }
    }

    public Task<TdsWorkItem> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken).AsTask();

    public TdsJobDto? Get(Guid jobId, Guid companyId, Guid requestedBy)
    {
        lock (_sync)
        {
            Cleanup(options.Value);
            return FindOwned(jobId, companyId, requestedBy)?.Dto;
        }
    }

    public byte[]? GetExcel(Guid jobId, Guid companyId, Guid requestedBy)
    {
        lock (_sync)
        {
            Cleanup(options.Value);
            return FindOwned(jobId, companyId, requestedBy)?.Excel;
        }
    }

    public bool Remove(Guid jobId, Guid companyId, Guid requestedBy)
    {
        lock (_sync)
        {
            var entry = FindOwned(jobId, companyId, requestedBy);
            return entry is not null && entry.Dto.Status is "completed" or "failed" && _jobs.Remove(jobId);
        }
    }

    public void SetProgress(Guid jobId, int totalFiles, int processedFiles, int? sourceTotalFiles = null, int? sourceOffset = null)
    {
        lock (_sync) if (_jobs.TryGetValue(jobId, out var entry))
        {
            entry.Dto = entry.Dto with { Status = "running", TotalFiles = totalFiles, ProcessedFiles = processedFiles };
            if (sourceTotalFiles is { } available && sourceOffset is { } offset)
                entry.Dto = entry.Dto with { SourceTotalFiles = available, SourceOffset = offset,
                    NextSourceOffset = offset + totalFiles < available ? offset + totalFiles : null };
        }
    }

    public void Complete(Guid jobId, byte[] excel)
    {
        lock (_sync) if (_jobs.TryGetValue(jobId, out var entry))
        {
            entry.Excel = excel;
            entry.Dto = entry.Dto with { Status = "completed", CompletedAt = DateTimeOffset.UtcNow };
        }
    }

    public void Fail(Guid jobId, string code)
    {
        lock (_sync) if (_jobs.TryGetValue(jobId, out var entry))
            entry.Dto = entry.Dto with { Status = "failed", ErrorCode = code, CompletedAt = DateTimeOffset.UtcNow };
    }

    private Entry? FindOwned(Guid id, Guid company, Guid user)
        => _jobs.TryGetValue(id, out var entry) && entry.CompanyId == company && entry.RequestedBy == user ? entry : null;

    private void Cleanup(TdsExtractionOptions settings)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-settings.RetentionMinutes);
        foreach (var id in _jobs.Where(x => x.Value.Dto.CompletedAt < cutoff).Select(x => x.Key).ToArray()) _jobs.Remove(id);
    }

    private sealed class Entry(Guid companyId, Guid requestedBy, TdsJobDto dto)
    {
        public Guid CompanyId { get; } = companyId;
        public Guid RequestedBy { get; } = requestedBy;
        public TdsJobDto Dto { get; set; } = dto;
        public byte[]? Excel { get; set; }
    }
}
