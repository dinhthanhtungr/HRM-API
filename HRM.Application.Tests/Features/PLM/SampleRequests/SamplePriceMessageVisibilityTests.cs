using System.Text.Json;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePriceMessageVisibilityTests
{
    private const string Secret = "SECRET_PRICE_35000";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingPublishedPriceContentIsMaskedAcrossSerializedThreadRepliesAndNotification(bool canViewPrice)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IInternalMailDbContext>();
        var messageId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var originalPayload = "{\"note\":\"" + Secret + "\",\"priceReference\":\"" + Secret + "\"}";
        store.Seed(nameof(db.SampleRequestWorkflowEvents), new SampleRequestWorkflowEvent
        {
            CompanyId = user.CompanyId!.Value, EventType = SampleRequestWorkflowEvents.PriceAccepted,
            MessageId = messageId, NotificationId = notificationId, Note = Secret
        });
        var areas = new InternalMailAreaAccessService(db, user, pricingVisibility: new Pricing(canViewPrice));
        var message = new InternalMessageDto { MessageId = messageId, Body = Secret, PayloadJson = originalPayload,
            References = [new InternalMessageReferenceDto { RelatedNameSnapshot = Secret, SnapshotJson = originalPayload }] };
        var reply = new InternalMessageDto { MessageId = Guid.NewGuid(), Body = "Ordinary reply",
            ReplyTo = new InternalMessageReplyDto { MessageId = messageId, BodyPreview = Secret } };
        var notification = new NotificationDto { Id = notificationId, Title = Secret, Message = Secret, PayloadJson = originalPayload };
        var preview = new InternalConversationListItemDto { LastMessageId = messageId, LastMessageBody = Secret };

        await areas.MaskPriceMessagesAsync([message, reply], default);
        await areas.MaskPriceNotificationsAsync([notification], default);
        await areas.MaskPriceConversationPreviewsAsync([preview], default);

        var json = JsonSerializer.Serialize(new { message, reply, notification, preview });
        Assert.Equal(canViewPrice, json.Contains(Secret, StringComparison.Ordinal));
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal(notificationId, notification.Id);
        Assert.Equal("Ordinary reply", reply.Body);
        // Visibility never rewrites the source event or persisted payload.
        Assert.Equal(Secret, db.SampleRequestWorkflowEvents.Single().Note);
    }

    [Fact]
    public async Task MissingPricingDecisionDeniesAndForeignCompanyEventsDoNotInfluenceCurrentContent()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabAdmin");
        var (db, store) = ProductionOrderTestStore.CreateContext<IInternalMailDbContext>();
        var own = Guid.NewGuid(); var foreign = Guid.NewGuid();
        store.Seed(nameof(db.SampleRequestWorkflowEvents),
            new SampleRequestWorkflowEvent { CompanyId = user.CompanyId!.Value, EventType = SampleRequestWorkflowEvents.PriceAccepted, MessageId = own },
            new SampleRequestWorkflowEvent { CompanyId = Guid.NewGuid(), EventType = SampleRequestWorkflowEvents.PriceAccepted, MessageId = foreign });
        var areas = new InternalMailAreaAccessService(db, user);
        var price = new InternalMessageDto { MessageId = own, Body = Secret };
        var regular = new InternalMessageDto { MessageId = foreign, Body = "Company-scoped ordinary content" };
        await areas.MaskPriceMessagesAsync([price, regular], default);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(price));
        Assert.Equal("Company-scoped ordinary content", regular.Body);
    }

    private sealed class Pricing(bool allowed) : IPricingVisibilityService
    {
        public PricingAccessDecision GetAccess() => new(false, allowed, false, false, false, false, false, false, false);
    }

    [Fact]
    public async Task ExplicitSharedCompanionRemainsVisibleWhileHistoricalPrivatePriceContentIsHidden()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IInternalMailDbContext>();
        var primaryId = Guid.NewGuid(); var companionId = Guid.NewGuid();
        store.Seed(nameof(db.SampleRequestWorkflowEvents),
            new SampleRequestWorkflowEvent { CompanyId = user.CompanyId!.Value,
                EventType = SampleRequestWorkflowEvents.PriceAccepted, MessageId = primaryId },
            new SampleRequestWorkflowEvent { CompanyId = user.CompanyId.Value,
                EventType = "CustomerPriceFeedbackShared", MessageId = companionId });
        var areas = new InternalMailAreaAccessService(db, user, pricingVisibility: new Pricing(false));
        var price = new InternalMessageDto { MessageId = primaryId, Body = Secret, PayloadJson = "{\"note\":\"" + Secret + "\"}" };
        var shared = new InternalMessageDto { MessageId = companionId, Body = "PUBLIC_CUSTOMER_FEEDBACK" };
        await areas.MaskPriceMessagesAsync([price, shared], default);
        var serialized = JsonSerializer.Serialize(new[] { price, shared });
        Assert.DoesNotContain(Secret, serialized);
        Assert.Contains("PUBLIC_CUSTOMER_FEEDBACK", serialized);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("Developer")]
    public async Task PrivateRecipientsUseRuntimeRoleClaimsForCustomGrantAndBaselineRevocation(string revokedRole)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("Developer");
        var (db, store) = ProductionOrderTestStore.CreateContext<IInternalMailDbContext>();
        var custom = Guid.NewGuid(); var revokedSale = Guid.NewGuid(); var fallbackPresident = Guid.NewGuid(); var foreign = Guid.NewGuid();
        var customRole = new ApplicationRole { Id = Guid.NewGuid(), Name = "CUSTOM_PRICE_READER" };
        var saleRole = new ApplicationRole { Id = Guid.NewGuid(), Name = revokedRole };
        var presidentRole = new ApplicationRole { Id = Guid.NewGuid(), Name = "President" };
        var employees = new[] { custom, revokedSale, fallbackPresident, foreign }.Select(id => new Employee
        { EmployeeId = id, CompanyId = id == foreign ? Guid.NewGuid() : user.CompanyId, IsActive = true }).ToArray();
        var accounts = employees.Select(e => new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = e.EmployeeId }).ToArray();
        var roleIds = new[] { customRole.Id, saleRole.Id, presidentRole.Id, customRole.Id };
        store.Seed(nameof(db.Employees), employees);
        store.Seed(nameof(db.Users), accounts);
        store.Seed(nameof(db.Roles), customRole, saleRole, presidentRole);
        store.Seed(nameof(db.UserRoles), accounts.Select((a, i) => new ApplicationUserRole
        { UserId = a.Id, RoleId = roleIds[i], IsActive = true }).ToArray());
        store.Seed(nameof(db.RoleClaims),
            new IdentityRoleClaim<Guid> { RoleId = customRole.Id, ClaimType = ApplicationPermissionClaimTypes.PermissionModelVersion, ClaimValue = "1" },
            new IdentityRoleClaim<Guid> { RoleId = customRole.Id, ClaimType = ApplicationPermissionClaimTypes.Permission, ClaimValue = ApplicationPermissions.Pricing.ViewApprovedSellingPrice },
            new IdentityRoleClaim<Guid> { RoleId = saleRole.Id, ClaimType = ApplicationPermissionClaimTypes.PermissionModelVersion, ClaimValue = "1" });
        var areas = new InternalMailAreaAccessService(db, user);
        var actual = await areas.PricingCapableEmployeesAsync(user.CompanyId!.Value,
            [custom, revokedSale, fallbackPresident, foreign], default);
        Assert.Equal(new[] { custom, fallbackPresident }.Order(), actual.Order());
    }
}
