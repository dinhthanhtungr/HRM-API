using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Pricing.Services;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.MessageRecipients.Services;
using HRM.Application.Features.PLM.SampleRequests.Attachments;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.CRM.Quotations.Services.Queries;
using HRM.Application.Features.CRM.InteractionSummaries.Services.Automation;
using HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;
using HRM.Application.Features.Timeline.Services;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Features.Work.MyTasks;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace HRM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddMediatR(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<InternalConversationSampleRequestInfoResolver>();
        services.AddScoped<InternalConversationQuotationInfoResolver>();
        services.AddScoped<IInternalConversationAccessService, InternalConversationAccessService>();
        services.AddScoped<InternalMailAreaAccessService>();
        services.AddScoped<ISampleRequestAttachmentService, SampleRequestAttachmentService>();
        services.AddScoped<ICustomerVisibilityService, CustomerVisibilityService>();
        services.AddScoped<IWarehouseStockVisibilityService, WarehouseStockVisibilityService>();
        services.AddScoped<CustomerCrmAccessService>();
        services.AddScoped<CustomerInteractionAiSummaryGenerationService>();
        services.AddScoped<
            ICustomerInteractionAiSummaryAutomationProcessor,
            CustomerInteractionAiSummaryAutomationProcessor>();
        services.AddScoped<CustomerFollowUpTaskCommandSupport>();
        services.AddScoped<MyTaskSupport>();
        services.AddScoped<CustomerTaxCodeConflictService>();
        services.AddScoped<CustomerActivationService>();
        services.AddScoped<ISaleGroupRecipientResolver, SaleGroupRecipientResolver>();
        services.AddScoped<QuotationLineBuilder>();
        services.AddScoped<QuotationHeaderUpdateService>();
        services.AddScoped<QuotationAtomicSaveService>();
        services.AddScoped<QuotationLinesReplaceService>();
        services.AddScoped<QuotationCustomerPriceUpdateService>();
        services.AddScoped<QuotationCurrentPricingResolver>();
        services.AddScoped<ProductPricingRealtimeSourceQueryService>();
        services.AddScoped<ProductPricingSourceQueryService>();
        services.AddScoped<StandardPriceRealtimeComparisonQueryService>();
        services.AddScoped<ProductPricingRequestQueryService>();
        services.AddScoped<ProductPricingApprovalNotificationService>();
        services.AddScoped<QuotationPricingApprovalStateService>();
        services.AddScoped<ProductStandardPriceReviewQueryService>();
        services.AddScoped<IQuotationPricingExpiryReminderProcessor, QuotationPricingExpiryReminderProcessor>();
        services.AddScoped<ProductPricingSourceValidator>();
        services.AddScoped<ProductPricingReviewReader>();
        services.AddScoped<SuggestedPricingFormulaQueryService>();
        services.AddScoped<ProductPricingReviewMaterialReader>();
        services.AddScoped<ProductPricingReviewCalculator>();
        services.AddScoped<ProductPricingReviewWriter>();
        services.AddScoped<FormulaPricingPolicyProvider>();
        services.AddScoped<IFormulaPricingPolicyResolver>(provider =>
            provider.GetRequiredService<FormulaPricingPolicyProvider>());
        services.AddScoped<FormulaPricingEngine>();
        services.AddScoped<ApprovedProductPricingTierReader>();
        services.AddScoped<SystemCalculatedProductPricingTierResolver>();
        services.AddScoped<LatestQuotationPricingTierReader>();
        services.AddScoped<QuotationProductTierPricingResolver>();
        services.AddScoped<QuotationManualPricingTemplateResolver>();
        services.AddScoped<QuotationTierPriceReferenceService>();
        services.AddScoped<QuotationConversationSubjectService>();
        services.AddScoped<DraftQuotationProductSnapshotSyncService>();
        services.AddSingleton<KeyedMutationLock<Guid>>();
        services.AddSingleton<KeyedMutationLock<string>>();
        services.AddScoped<ICustomerCrmWorkService, CustomerCrmWorkService>();
        services.AddScoped<ICustomerCrmAnalyticsService, CustomerCrmAnalyticsService>();
        services.AddScoped<ICustomerFollowUpTaskDueReminderProcessor, CustomerFollowUpTaskDueReminderProcessor>();
        services.AddScoped<IPLMFieldVisibilityService, PLMFieldVisibilityService>();
        services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
        services.AddScoped<HRM.Application.Features.MRO.Equipment.Services.IEquipmentManagementService,
            HRM.Application.Features.MRO.Equipment.Services.EquipmentManagementService>();
        services.AddScoped<IPricingVisibilityService, PricingVisibilityService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEventLogWriter, EventLogWriter>();
        services.AddScoped<IMessageRecipientResolver, SampleRequestMessageRecipientResolver>();
        services.AddScoped<SampleRequestRecipientResolver>();
        services.AddScoped<SampleRequestConversationSubjectService>();
        services.AddScoped<FormulaWriteService>();
        services.AddScoped<FormulaVersionService>();
        services.AddScoped<FormulaPricingReviewService>();
        services.AddScoped<FormulaListQueryService>();
        services.AddScoped<SaleOrderLeadConversionService>();
        services.AddScoped<SaleOrderCreationService>();
        services.AddScoped<SaleOrderManufacturingService>();
        services.AddScoped<HRM.Application.Features.PLM.ProductionOrders.Services.ProductionOrderReferenceValidator>();
        services.AddScoped<HRM.Application.Features.PLM.ProductionOrders.Services.ProductionOrderReservationService>();
        services.AddScoped<SaleOrderApprovalService>();
        services.AddScoped<ComplaintInteractionWriter>();
        services.AddScoped<ComplaintReceptionResolver>();
        services.AddScoped<ComplaintHandlingOrderService>();
        services.AddScoped<ComplaintEventLogWriter>();
        services.AddScoped<ComplaintDecisionNotificationService>();
        services.AddScoped<ComplaintReportAccessService>();
        services.AddScoped<BomItemResolver>();
        services.AddScoped<BomLifecycleService>();
        services.AddScoped<ManufacturingBomStructureService>();
        services.AddScoped<ManufacturingLossProfileWriteService>();
        services.AddScoped<DeliveryOrderLotInventoryService>();
        services.AddScoped<PurchaseOrderWorkflowService>();
        services.AddScoped<PurchaseOrderReceiptReader>();

        return services;
    }
}
