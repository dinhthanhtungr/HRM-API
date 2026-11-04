using HRM.Application.Features.PLM.Materials.TdsExtraction;
using HRM.Application.Abstractions.Commons.Ais.CRM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Services.Geminis.Materials;

internal static class TdsExtractionDependencyInjection
{
    internal static IServiceCollection AddTdsExtraction(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TdsExtractionOptions>().Bind(configuration.GetSection(TdsExtractionOptions.SectionName))
            .Validate(x => x.MaxFiles is >= 1 and <= 100 && x.MaxFileBytes is >= 1 and <= 10 * 1024 * 1024 &&
                x.MaxBatchBytes >= x.MaxFileBytes && x.MaxBatchBytes <= 40 * 1024 * 1024 &&
                x.RetentionMinutes is >= 1 and <= 1440 && x.MaxRetainedJobs is >= 1 and <= 8 &&
                x.MaxTextCharacters is >= 100 and <= 500_000, "Invalid MaterialTdsExtraction limits.");
        services.AddSingleton<ITdsJobQueue, TdsJobQueue>();
        services.AddScoped<TdsExtractionAccess>();
        services.AddScoped<TdsExtractionProcessor>();
        services.AddScoped<TdsLegacyOfficeConverter>();
        services.AddScoped<ITdsDocumentNormalizer, TdsDocumentNormalizer>();
        services.AddSingleton<ITdsExcelRenderer, TdsExcelRenderer>();
        services.AddScoped<ITdsExtractionClient>(provider => new GeminiTdsExtractionClient(
            new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
            provider.GetRequiredService<IOptions<GeminiOptions>>(), provider.GetRequiredService<IGeminiRateLimitService>()));
        return services;
    }
}
