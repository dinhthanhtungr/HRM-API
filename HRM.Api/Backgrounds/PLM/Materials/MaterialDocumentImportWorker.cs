using HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

namespace HRM.Api.Backgrounds.PLM.Materials;

/// <summary>
/// Xử lý tuần tự các job import tài liệu NVL để tránh hai job cùng ghi vào
/// một AttachmentCollection. Job và tiến độ hiện được giữ trong bộ nhớ.
/// </summary>
public sealed class MaterialDocumentImportWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMaterialDocumentImportJobQueue _jobQueue;
    private readonly ILogger<MaterialDocumentImportWorker> _logger;

    public MaterialDocumentImportWorker(
        IServiceScopeFactory scopeFactory,
        IMaterialDocumentImportJobQueue jobQueue,
        ILogger<MaterialDocumentImportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            MaterialDocumentImportJobWorkItem job;
            try
            {
                job = await _jobQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<IMaterialDocumentImportProcessor>();
                await processor.ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _jobQueue.MarkFailed(job.JobId, "worker_stopped");
                return;
            }
            catch (Exception exception)
            {
                _jobQueue.MarkFailed(job.JobId, "job_processing_failed");
                _logger.LogError(
                    exception,
                    "Material document import job {JobId} failed.",
                    job.JobId);
            }
        }
    }
}
