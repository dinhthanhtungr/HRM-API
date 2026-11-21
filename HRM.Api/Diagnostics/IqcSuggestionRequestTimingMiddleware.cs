using System.Diagnostics;
using System.Globalization;
using HRM.Application.Features.DevAndQA.IQC.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRM.Api.Diagnostics;

/// <summary>Chỉ đo GET gợi ý IQC trong Development, không ghi tham số, SQL, ID hoặc nội dung nghiệp vụ.</summary>
public sealed class IqcSuggestionRequestTimingMiddleware(
    RequestDelegate next, IHostEnvironment environment, ILogger<IqcSuggestionRequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (!environment.IsDevelopment() || !HttpMethods.IsGet(context.Request.Method) ||
            !context.Request.Query.ContainsKey("suggestionKeyword") ||
            !(path.Equals(new PathString("/api/v1/dev-and-qa/iqc")) ||
              path.Equals(new PathString("/api/v1/dev-and-qa/iqc/GetPaged")) ||
              path.Equals(new PathString("/api/iqcinput")) ||
              path.Equals(new PathString("/api/iqcinput/GetPaged"))))
        {
            await next(context);
            return;
        }

        using var activity = new Activity(IqcSuggestionDiagnosticTags.ActivityName).Start();
        activity.SetTag(IqcSuggestionDiagnosticTags.Enabled, true);
        var watch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            if (context.User.Identity?.IsAuthenticated == true && context.Response.StatusCode == StatusCodes.Status200OK)
            {
                var metrics = new List<string>();
                if (activity.GetTagItem(IqcSuggestionDiagnosticTags.SuggestionMilliseconds) is long queryMs && queryMs >= 0)
                    metrics.Add("iqc_suggestions;dur=" + queryMs.ToString(CultureInfo.InvariantCulture));
                metrics.Add("iqc_total;dur=" + watch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                context.Response.Headers.Append("Server-Timing", string.Join(", ", metrics));
            }
            return Task.CompletedTask;
        });

        var outcome = "completed";
        try { await next(context); }
        catch (OperationCanceledException)
        {
            outcome = "canceled";
            throw;
        }
        catch
        {
            outcome = "faulted";
            throw;
        }
        finally
        {
            watch.Stop();
            if (context.User.Identity?.IsAuthenticated == true)
            {
                if (context.RequestAborted.IsCancellationRequested) outcome = "canceled";
                // Request bị client hủy không có response/header để đọc: vẫn ghi thời lượng và trạng thái an toàn.
                logger.LogInformation("IQC suggestion timing TotalMs={TotalMs} SuggestionMs={SuggestionMs} Outcome={Outcome} StatusCode={StatusCode}",
                    watch.ElapsedMilliseconds,
                    activity.GetTagItem(IqcSuggestionDiagnosticTags.SuggestionMilliseconds) as long?,
                    outcome, context.Response.HasStarted ? context.Response.StatusCode : 0);
            }
        }
    }
}
