using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using Microsoft.Extensions.Options;
namespace HRM.Api.Backgrounds.Notifications;
public sealed class SampleRequestConversationSummaryWorker(IServiceScopeFactory scopes,
    IOptions<ConversationSummaryOptions> options, ILogger<SampleRequestConversationSummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Value.Enabled)
                {
                    // Sequential work only; separate scopes prevent tracked rows accumulating.
                    for (var i = 0; i < 5; i++)
                    {
                        using var scope = scopes.CreateScope();
                        if (!await scope.ServiceProvider.GetRequiredService<ISampleRequestConversationSummaryStore>().ProcessOneAsync(stoppingToken)) break;
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                logger.LogWarning("Sample summary synchronization failed; pending work will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
