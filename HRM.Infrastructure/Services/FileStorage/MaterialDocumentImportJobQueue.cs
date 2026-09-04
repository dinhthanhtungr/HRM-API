using System.Collections.Concurrent;
using System.Threading.Channels;
using HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

namespace HRM.Infrastructure.Services.FileStorage;

internal sealed class MaterialDocumentImportJobQueue
    : IMaterialDocumentImportJobQueue
{
    private const int MaxStoredExceptionsPerJob = 5_000;

    private readonly Channel<MaterialDocumentImportJobWorkItem> _channel =
        Channel.CreateUnbounded<MaterialDocumentImportJobWorkItem>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    private readonly ConcurrentDictionary<Guid, JobState> _jobs = new();
    private readonly Dictionary<Guid, Guid> _activeJobByCompany = [];
    private readonly object _activeJobsLock = new();

    public bool TryEnqueue(
        Guid companyId,
        Guid requestedByUserId,
        Guid? requestedByEmployeeId,
        out MaterialDocumentImportJobSnapshot job)
    {
        lock (_activeJobsLock)
        {
            if (_activeJobByCompany.TryGetValue(companyId, out var activeJobId) &&
                _jobs.TryGetValue(activeJobId, out var activeState))
            {
                job = activeState.CreateSnapshot();
                return false;
            }

            var workItem = new MaterialDocumentImportJobWorkItem(
                Guid.CreateVersion7(),
                companyId,
                requestedByUserId,
                requestedByEmployeeId);
            var state = new JobState(workItem);
            if (!_jobs.TryAdd(workItem.JobId, state) || !_channel.Writer.TryWrite(workItem))
            {
                throw new InvalidOperationException("Could not enqueue material document import job.");
            }

            _activeJobByCompany[companyId] = workItem.JobId;
            job = state.CreateSnapshot();
            return true;
        }
    }

    public ValueTask<MaterialDocumentImportJobWorkItem> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public MaterialDocumentImportJobSnapshot? Get(Guid jobId, Guid companyId)
    {
        return _jobs.TryGetValue(jobId, out var state) &&
               state.WorkItem.CompanyId == companyId
            ? state.CreateSnapshot()
            : null;
    }

    public IReadOnlyList<MaterialDocumentImportJobException> GetExceptions(
        Guid jobId,
        Guid companyId)
    {
        return _jobs.TryGetValue(jobId, out var state) &&
               state.WorkItem.CompanyId == companyId
            ? state.GetExceptions()
            : [];
    }

    public void MarkRunning(Guid jobId, int totalFiles)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        lock (state.SyncRoot)
        {
            state.Status = MaterialDocumentImportJobStatuses.Running;
            state.StartedAt ??= DateTimeOffset.Now;
            state.TotalFiles = Math.Max(0, totalFiles);
            state.ErrorCode = null;
        }
    }

    public void RecordItem(
        Guid jobId,
        MaterialDocumentImportItemOutcome outcome,
        MaterialDocumentImportJobException? exception = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        lock (state.SyncRoot)
        {
            state.ProcessedFiles++;
            switch (outcome)
            {
                case MaterialDocumentImportItemOutcome.Imported:
                    state.ImportedFiles++;
                    break;
                case MaterialDocumentImportItemOutcome.Duplicate:
                    state.DuplicateFiles++;
                    break;
                case MaterialDocumentImportItemOutcome.RequiresReview:
                    state.RequiresReviewFiles++;
                    break;
                case MaterialDocumentImportItemOutcome.Failed:
                    state.FailedFiles++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }

            if (exception is null)
            {
                return;
            }

            state.ExceptionCount++;
            if (state.Exceptions.Count < MaxStoredExceptionsPerJob)
            {
                state.Exceptions.Add(exception);
            }
            else
            {
                state.ExceptionsTruncated = true;
            }
        }
    }

    public void MarkCompleted(Guid jobId)
    {
        MarkTerminal(jobId, MaterialDocumentImportJobStatuses.Completed, null);
    }

    public void MarkFailed(Guid jobId, string errorCode)
    {
        MarkTerminal(jobId, MaterialDocumentImportJobStatuses.Failed, errorCode);
    }

    private void MarkTerminal(Guid jobId, string status, string? errorCode)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        lock (state.SyncRoot)
        {
            state.Status = status;
            state.CompletedAt = DateTimeOffset.Now;
            state.ErrorCode = errorCode;
        }

        lock (_activeJobsLock)
        {
            if (_activeJobByCompany.TryGetValue(
                    state.WorkItem.CompanyId,
                    out var activeJobId) &&
                activeJobId == jobId)
            {
                _activeJobByCompany.Remove(state.WorkItem.CompanyId);
            }
        }
    }

    private sealed class JobState
    {
        public JobState(MaterialDocumentImportJobWorkItem workItem)
        {
            WorkItem = workItem;
        }

        public object SyncRoot { get; } = new();
        public MaterialDocumentImportJobWorkItem WorkItem { get; }
        public string Status { get; set; } = MaterialDocumentImportJobStatuses.Queued;
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int ImportedFiles { get; set; }
        public int DuplicateFiles { get; set; }
        public int RequiresReviewFiles { get; set; }
        public int FailedFiles { get; set; }
        public int ExceptionCount { get; set; }
        public bool ExceptionsTruncated { get; set; }
        public string? ErrorCode { get; set; }
        public List<MaterialDocumentImportJobException> Exceptions { get; } = [];

        public MaterialDocumentImportJobSnapshot CreateSnapshot()
        {
            lock (SyncRoot)
            {
                return new MaterialDocumentImportJobSnapshot(
                    WorkItem.JobId,
                    WorkItem.CompanyId,
                    Status,
                    CreatedAt,
                    StartedAt,
                    CompletedAt,
                    TotalFiles,
                    ProcessedFiles,
                    ImportedFiles,
                    DuplicateFiles,
                    RequiresReviewFiles,
                    FailedFiles,
                    ExceptionCount,
                    ExceptionsTruncated,
                    ErrorCode);
            }
        }

        public IReadOnlyList<MaterialDocumentImportJobException> GetExceptions()
        {
            lock (SyncRoot)
            {
                return Exceptions.ToArray();
            }
        }
    }
}
