using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Persistence.Executive;
using HRM.Application.Abstractions.Persistence.HRM;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.NotificationHub;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Persistence.Purchasing;
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
        services.Configure<HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups.CustomerSampleSharingOptions>(configuration.GetSection("CustomerSampleSharing"));
        services.Configure<HRM.Application.Features.PLM.SaleOrders.DeliverySchedules.DeliveryScheduleOptions>(
            configuration.GetSection(HRM.Application.Features.PLM.SaleOrders.DeliverySchedules.DeliveryScheduleOptions.SectionName));
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
        services.AddScoped<HRM.Application.Features.UserPreferences.Services.IUserPreferenceStore,
            HRM.Infrastructure.Persistence.Preferences.UserPreferenceStore>();
        QuestPDF.Settings.License = questPdfLicense;
        services.AddSingleton<IQuotationPdfRenderer, QuotationPdfRenderer>();
        services.AddSingleton<IManufacturingVUFormulaPdfRenderer, ManufacturingVUFormulaPdfRenderer>();
        services.AddSingleton<IManufacturingAdjustmentPdfRenderer, ManufacturingAdjustmentPdfRenderer>();
        services.AddSingleton<IManufacturingOrderPdfRenderer, ManufacturingOrderPdfRenderer>();
        services.AddSingleton<IComplaintReportPdfRenderer, ComplaintReportPdfRenderer>();
        services.AddSingleton<IProductInspectionPdfRenderer, ProductInspectionPdfRenderer>();
        services.AddSingleton<IColorChipRecordPdfRenderer, ColorChipRecordPdfRenderer>();
        services.AddSingleton<IFormulaMaterialsExcelRenderer, FormulaMaterialsExcelRenderer>();
        services.AddSingleton<IStockAvailableExcelRenderer, StockAvailableExcelRenderer>();
        services.AddSingleton<IProductColourNamePreviewExcelRenderer, ProductColourNamePreviewExcelRenderer>();
        services.AddSingleton<IProductionOrderQuantityExcelRenderer, ProductionOrderQuantityExcelRenderer>();
        services.AddSingleton<IManufacturingFormulaExcelRenderer, ManufacturingFormulaExcelRenderer>();
        services.AddSingleton<IMerchandiseOrderVaExcelRenderer, MerchandiseOrderVaExcelRenderer>();
        services.AddSingleton<IMaterialsExcelRenderer, MaterialsExcelRenderer>();
        services.AddSingleton<IMaterialUsageExcelRenderer, MaterialUsageExcelRenderer>();
        services.AddSingleton<IManufacturingVaExcelRenderer, ManufacturingVaExcelRenderer>();
        services.AddSingleton<IPurchaseOrderPdfRenderer, PurchaseOrderPdfRenderer>();
        services.AddSingleton<IPurchaseOrderExcelRenderer, PurchaseOrderExcelRenderer>();
        services.AddSingleton<IPLPUFinishReportExcelRenderer, ExportFinishReportExcelHelper>();
        services.AddSingleton<IDeliveryOrderPdfRenderer, DeliveryOrderPdfRenderer>();
        services.AddSingleton<IDeliveryOrderExcelRenderer, DeliveryOrderExcelRenderer>();

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
        services.AddScoped<HRM.Application.Abstractions.Persistence.MRO.IEquipmentDbContext>(
            provider => provider.GetRequiredService<ApplicationDbContext>());

        // Employee DbContexts
        services.AddScoped<IEmployeeReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IEmployeeManagementDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());


        // Dispatch DbContexts
        services.AddScoped<IDispatchReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDispatchWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules.IDeliveryScheduleDbContext>(
            provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<HRM.Application.Abstractions.Documents.IDeliveryReportExcelRenderer,
            HRM.Infrastructure.Documents.Excels.DeliveryReportExcelRenderer>();

        // CRM DbContexts
        services.AddScoped<ICRMReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICRMWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICustomerVisibilityReadDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // PLM DbContexts
        services.AddScoped<HRM.Application.Abstractions.Persistence.PLM.Manufacturing.IManufacturingDbContext>(
            provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPLMReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPLMWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IMaterialProcessingDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<HRM.Application.Abstractions.Persistence.PLM.ProductionOrders.IProductionOrderDbContext>(
            provider => provider.GetRequiredService<ApplicationDbContext>());

        // PLM Sale Order DbContext
        services.AddScoped<ISaleOrderDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<HRM.Application.Abstractions.Persistence.CRM.CustomerCare.ICustomerSampleSharingDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPurchaseOrderDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IComplaintReportDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<HRM.Application.Abstractions.Persistence.DevAndQA.IEvaluationDbContext>(
            sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<HRM.Application.Abstractions.Persistence.DevAndQA.ILabTestDbContext>(
            sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<HRM.Application.Abstractions.Documents.ILabTestSheetReader,
            HRM.Infrastructure.Documents.Excels.LabTests.LabTestSheetReader>();
        services.AddScoped<HRM.Application.Abstractions.Persistence.DevAndQA.IIqcDbContext>(
            provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<HRM.Application.Abstractions.Persistence.DevAndQA.IEvaluationFileSaveDbContext>(
            provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<HRM.Application.Abstractions.Documents.IIqcEvaluationExcelRenderer,
            HRM.Infrastructure.Documents.Excels.IQC.IqcEvaluationExcelRenderer>();
        services.AddSingleton<HRM.Application.Abstractions.Documents.IIqcExcelRenderer,
            HRM.Infrastructure.Documents.Excels.IQC.IqcExcelRenderer>();

        // Warehouse DbContexts
        services.AddScoped<IWarehouseReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Notification DbContext
        services.AddScoped<INotificationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<HRM.Application.Abstractions.Notifications.INotificationInboxArchiver,
            HRM.Infrastructure.Services.Notifications.NotificationInboxArchiver>();

        // Notification Hub write DbContext
        services.AddScoped<INotificationHubWriteDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Internal mail DbContext
        services.AddScoped<IInternalMailDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Executive read models
        services.AddScoped<IExecutiveReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Report DbContexts
        services.AddSingleton<HRM.Application.Features.Reports.CompanyOverview.ExportCompanyOverview.ICompanyOverviewExcelRenderer,
            HRM.Infrastructure.Documents.Excels.CompanyOverviewExcelRenderer>();
        services.AddScoped<HRM.Application.Features.Reports.CompanyOverview.ICompanyOverviewReader,
            HRM.Infrastructure.Services.Reports.CompanyOverviewReader>();
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
