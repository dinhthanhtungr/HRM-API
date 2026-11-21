using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

internal static class PromisedDeliveryTestFactory
{
    public static (PromisedDeliveryNotifier Notifier, NotifyPromisedDeliveryTests.CapturePort Capture) Create(
        IManufacturingDbContext db, ICurrentUser user)
    {
        var notifications = DispatchProxy.Create<INotificationService, NotifyPromisedDeliveryTests.CapturePort>();
        var groups = DispatchProxy.Create<ISaleGroupRecipientResolver, NotifyPromisedDeliveryTests.CapturePort>();
        ((NotifyPromisedDeliveryTests.CapturePort)(object)groups).Leader = Guid.NewGuid();
        return (new(db, user, groups, notifications), (NotifyPromisedDeliveryTests.CapturePort)(object)notifications);
    }
}
