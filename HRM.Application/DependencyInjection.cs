using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.MessageRecipients.Services;
using HRM.Application.Features.PLM.SampleRequests.Attachments;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.CRM.InteractionSummaries.Services.Automation;
using HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;
using HRM.Application.Features.Timeline.Services;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Work.MyTasks;
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
        services.AddScoped<ISampleRequestAttachmentService, SampleRequestAttachmentService>();
        services.AddScoped<ICustomerVisibilityService, CustomerVisibilityService>();
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
        services.AddScoped<QuotationCurrentPricingResolver>();
        services.AddScoped<QuotationConversationSubjectService>();
        services.AddSingleton<KeyedMutationLock<Guid>>();
        services.AddScoped<ICustomerCrmWorkService, CustomerCrmWorkService>();
        services.AddScoped<ICustomerCrmAnalyticsService, CustomerCrmAnalyticsService>();
        services.AddScoped<ICustomerFollowUpTaskDueReminderProcessor, CustomerFollowUpTaskDueReminderProcessor>();
        services.AddScoped<IPLMFieldVisibilityService, PLMFieldVisibilityService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEventLogWriter, EventLogWriter>();
        services.AddScoped<IMessageRecipientResolver, SampleRequestMessageRecipientResolver>();
        services.AddScoped<SampleRequestRecipientResolver>();
        services.AddScoped<SampleRequestConversationSubjectService>();
        services.AddScoped<FormulaWriteService>();
        services.AddScoped<SaleOrderLeadConversionService>();
        services.AddScoped<SaleOrderCreationService>();
        services.AddScoped<SaleOrderManufacturingService>();
        services.AddScoped<SaleOrderApprovalService>();
        services.AddScoped<ComplaintInteractionWriter>();
        services.AddScoped<ComplaintReceptionResolver>();
        services.AddScoped<ComplaintHandlingOrderService>();
        services.AddScoped<ComplaintEventLogWriter>();
        services.AddScoped<ComplaintDecisionNotificationService>();
        services.AddScoped<ComplaintReportAccessService>();
        services.AddScoped<DeliveryOrderLotInventoryService>();

        return services;
    }
}
