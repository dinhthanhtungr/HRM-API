using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("Developer", false, true)]
    [InlineData("President", false, false)]
    [InlineData("SaleUser", true, true)]
    [InlineData("SaleUser", false, false)]
    [InlineData("LabUser", false, false)]
    public async Task FeedMetadataUsesOnlyReadableConversationsInTheRequestedPage(string role, bool member, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, member);
        var request = FeedSampleRequest(store, user);
        var resolver = new NotificationFeedConversationInfoResolver(store.Db,
            new InternalMailAreaAccessService(store.Db, user), new InternalConversationSampleRequestInfoResolver(store.Db), new InternalConversationQuotationInfoResolver(store.Db));
        var response = (await resolver.ResolveAsync([store.Child.InternalConversationId, store.Child.InternalConversationId,
            Guid.Empty, Guid.NewGuid()], user.CompanyId!.Value, default)).SampleRequests;
        Assert.Equal(expected ? 1 : 0, response.Count);
        if (expected)
        {
            var info = response[store.Child.InternalConversationId];
            Assert.Equal(request.ExternalId, info.RequestCode);
            Assert.Equal("Customer", info.CustomerName);
            Assert.Equal("Sale", info.SaleName);
            Assert.Equal("COMPOUND", info.ProductCategory?.Code);
        }
        Assert.False(response.ContainsKey(store.Root.InternalConversationId));
    }

    [Theory]
    [InlineData("foreign-conversation")]
    [InlineData("inactive-conversation")]
    [InlineData("inactive-parent")]
    [InlineData("foreign-request")]
    [InlineData("inactive-request")]
    [InlineData("foreign-customer")]
    [InlineData("foreign-sale")]
    public async Task FeedMetadataFailsClosedWithoutChangingTheSource(string boundary)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        var request = FeedSampleRequest(store, user);
        switch (boundary)
        {
            case "foreign-conversation": store.Child.CompanyId = Guid.NewGuid(); break;
            case "inactive-conversation": store.Child.IsActive = false; break;
            case "inactive-parent": store.Root.IsActive = false; break;
            case "foreign-request": request.CompanyId = Guid.NewGuid(); break;
            case "inactive-request": request.IsActive = false; break;
            case "foreign-customer": request.Customer.CompanyId = Guid.NewGuid(); break;
            case "foreign-sale": request.ManagerByNavigation.CompanyId = Guid.NewGuid(); break;
        }
        var sourceTime = store.Child.CreatedAt;
        var resolver = new NotificationFeedConversationInfoResolver(store.Db,
            new InternalMailAreaAccessService(store.Db, user), new InternalConversationSampleRequestInfoResolver(store.Db), new InternalConversationQuotationInfoResolver(store.Db));
        Assert.Empty((await resolver.ResolveAsync([store.Child.InternalConversationId], user.CompanyId!.Value, default)).SampleRequests);
        Assert.Equal(sourceTime, store.Child.CreatedAt);
    }

    private static SampleRequest FeedSampleRequest(
        (HRM.Application.Abstractions.Persistence.InternalMail.IInternalMailDbContext Db,
            HRM.Domain.Entities.InternalMailSchema.InternalConversation Root,
            HRM.Domain.Entities.InternalMailSchema.InternalConversation Child) store, User user)
    {
        var companyId = user.CompanyId!.Value;
        var request = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), CompanyId = companyId, IsActive = true, ExternalId = "TP_30991",
            Customer = new() { CompanyId = companyId, CustomerName = "Customer" },
            ManagerByNavigation = new() { CompanyId = companyId, FullName = "Sale" },
            Product = new() { CompanyId = companyId, IsActive = true, ColourCode = "LP12007C",
                Category = new() { CompanyId = companyId, IsActive = true, Types = "Product", ExternalId = "COMPOUND" } }
        };
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.RelatedId = request.SampleRequestId;
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.SampleRequests)] = new MemorySet<SampleRequest>([request]);
        return request;
    }
}
