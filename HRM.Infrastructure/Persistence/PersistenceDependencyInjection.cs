using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Persistence.HRM;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Persistence.Timeline;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Persistence.Work;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Documents.Pdfs;
using HRM.Infrastructure.Documents.Pdfs.ColorChipRecords;
using HRM.Infrastructure.Documents.Excels;
using HRM.Infrastructure.Services.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure;

internal static class PersistenceDependencyInjection
{
    internal static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var pdfSection = configuration.GetSection(PdfOptions.SectionName);
        services.Configure<PdfOptions>(pdfSection);

        var questPdfLicenseName = pdfSection[nameof(PdfOptions.QuestPdfLicense)]
            ?? nameof(LicenseType.Evaluation);
        if (!Enum.TryParse<LicenseType>(
                questPdfLicenseName,
                ignoreCase: true,
                out var questPdfLicense))
        {
            throw new InvalidOperationException(
                $"Pdf:QuestPdfLicense '{questPdfLicenseName}' is invalid.");
        }

        RegisterPdfFonts();
        QuestPDF.Settings.License = questPdfLicense;
        services.AddSingleton<IQuotationPdfRenderer, QuotationPdfRenderer>();
        services.AddSingleton<IManufacturingVUFormulaPdfRenderer, ManufacturingVUFormulaPdfRenderer>();
        services.AddSingleton<IComplaintReportPdfRenderer, ComplaintReportPdfRenderer>();
        services.AddSingleton<IProductInspectionPdfRenderer, ProductInspectionPdfRenderer>();
        services.AddSingleton<IColorChipRecordPdfRenderer, ColorChipRecordPdfRenderer>();
        services.AddSingleton<IFormulaMaterialsExcelRenderer, FormulaMaterialsExcelRenderer>();

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("AppDbConnectionString"));

            if (bool.TryParse(configuration["Database:EnableDetailedErrors"], out var enableDetailedErrors) &&
                enableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IHrDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Employee DbContexts
        services.AddScoped<IEmployeeReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IEmployeeManagementDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());


        // Dispatch DbContexts
        services.AddScoped<IDispatchReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDispatchWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // CRM DbContexts
        services.AddScoped<ICRMReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICRMWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICustomerVisibilityReadDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // PLM DbContexts
        services.AddScoped<IPLMReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPLMWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // PLM Sale Order DbContext
        services.AddScoped<ISaleOrderDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IComplaintReportDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Warehouse DbContexts
        services.AddScoped<IWarehouseReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Notification DbContext
        services.AddScoped<INotificationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Internal mail DbContext
        services.AddScoped<IInternalMailDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Report DbContexts
        services.AddScoped<IReportReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Timeline DbContext
        services.AddScoped<ITimelineDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Work task DbContexts
        services.AddScoped<IWorkTaskReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IWorkTaskWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());


        // ======================================= Commons =======================================
        services.AddScoped<IPriceReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IMaterialPriceQueryService, MaterialPriceQueryService>();
        return services;
    }

    private static void RegisterPdfFonts()
    {
        var openSansRegularPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Fonts",
            "OpenSans-Regular.ttf");

        if (!File.Exists(openSansRegularPath))
        {
            return;
        }

        using var fontStream = File.OpenRead(openSansRegularPath);
        FontManager.RegisterFont(fontStream);
    }
}
