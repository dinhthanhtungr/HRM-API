using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.SampleRequests.Attachments;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {

        services.AddMediatR(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ISampleRequestAttachmentService, SampleRequestAttachmentService>();
        services.AddScoped<ICustomerVisibilityService, CustomerVisibilityService>();
        services.AddScoped<CustomerCrmAccessService>();
        services.AddScoped<CustomerFollowUpTaskCommandSupport>();
        services.AddScoped<CustomerTaxCodeConflictService>();
        services.AddScoped<QuotationLineBuilder>();
        services.AddScoped<ICustomerCrmWorkService, CustomerCrmWorkService>();
        services.AddScoped<ICustomerCrmAnalyticsService, CustomerCrmAnalyticsService>();
        services.AddScoped<ICustomerFollowUpTaskDueReminderProcessor, CustomerFollowUpTaskDueReminderProcessor>();
        services.AddScoped<IPLMFieldVisibilityService, PLMFieldVisibilityService>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
