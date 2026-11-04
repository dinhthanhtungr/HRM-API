using HRM.Application.Features.PLM.Materials.TdsExtraction;

namespace HRM.Api.Backgrounds.PLM.Materials;

public sealed class MaterialTdsExtractionWorker(IServiceScopeFactory scopes, ITdsJobQueue queue,
    ILogger<MaterialTdsExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TdsWorkItem job;
            try { job = await queue.DequeueAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<TdsExtractionProcessor>().ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                queue.Fail(job.JobId, "worker_stopped");
                return;
            }
            catch (Exception exception)
            {
                var code = exception is TdsProcessingException known ? known.Code : "job_processing_failed";
                queue.Fail(job.JobId, code);
                logger.LogWarning("TDS extraction job {JobId} failed with {ErrorCode}.", job.JobId, code);
            }
        }
    }
}
