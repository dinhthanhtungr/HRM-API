using HRM.Application.Features.CRM.Quotations.Commands.RequestQuotation;
using HRM.Domain.Entities.Notifications;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public class RequestQuotationNotificationTests
{
    [Fact]
    public void PreviewRespectsStorageLimitIncludingPrefix()
    {
        const string prefix = "An yêu cầu báo giá cho Khách A: ";
        var fits = new string('a', Notification.MaxMessageLength - prefix.Length);

        Assert.Equal(prefix + fits, RequestQuotationCommandHandler.BuildNotificationPreview("An", "Khách A", fits));
        var longMessage = (fits[..^2] + "😀").PadRight(5000, 'b');
        Assert.Equal(prefix + fits[..^2] + "…",
            RequestQuotationCommandHandler.BuildNotificationPreview("An", "Khách A", longMessage));
    }
}
