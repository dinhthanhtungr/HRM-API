using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("President", InternalMailRelatedType.ConversationTechnical)]
    [InlineData("President", InternalMailRelatedType.ConversationPricing)]
    [InlineData("President", InternalMailRelatedType.ConversationCustom)]
    [InlineData("President", InternalMailRelatedType.SampleRequest)]
    [InlineData("ACCUser", InternalMailRelatedType.SampleRequest)]
    public void NonDeveloperInboxRequiresMembershipLikeDetail(string role, InternalMailRelatedType type)
    {
        var user = new User(role);
        var store = CreateStore(user, type, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.DoesNotContain(access.InboxConversations(false), c => c.InternalConversationId == store.Child.InternalConversationId);
        store.Child.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        var row = Assert.Single(access.InboxConversations(false).Where(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.True(row.IsMember);
        store.Child.CompanyId = Guid.NewGuid();
        Assert.DoesNotContain(access.InboxConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
    }

    [Fact]
    public async Task PresidentListRetainsCustomerAndSaleMetadataWithMembership()
    {
        var user = new User("President");
        var store = CreateStore(user, InternalMailRelatedType.Internal, false);
        var memory = (MemoryStore)(object)store.Db;
        var requestId = Guid.NewGuid();
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.RelatedId = requestId;
        store.Root.RelatedExternalId = "TP_TEST";
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        memory.Values[nameof(store.Db.InternalMessages)] = new MemorySet<InternalMessage>([]);
        memory.Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] = new AsyncQuery<InternalConversationNotificationUnreadCount>([]);
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, CustomerName = "Test customer", ExternalId = "TEST_CUSTOMER" };
        var sale = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, FullName = "Test sale" };
        memory.Values[nameof(store.Db.SampleRequests)] = new MemorySet<SampleRequest>([new()
        {
            SampleRequestId = requestId, CompanyId = user.CompanyId.Value, IsActive = true, ExternalId = "TP_TEST",
            CustomerId = customer.CustomerId, Customer = customer, ManagerBy = sale.EmployeeId, ManagerByNavigation = sale,
            Product = new Product
            {
                ProductId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true, ColourCode = "COLOR_TEST",
                Category = new()
                {
                    CategoryId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true,
                    Types = "Product", ExternalId = "CMP", Name = "Compound"
                }
            }
        }]);
        var handler = new GetInternalConversationsQueryHandler(store.Db, user,
            new InternalConversationSampleRequestInfoResolver(store.Db), new InternalConversationQuotationInfoResolver(store.Db),
            new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new() { RelatedType = InternalMailRelatedType.SampleRequest }, default);
        var item = Assert.Single(result.Items);
        Assert.Equal("Test customer", item.SampleRequestInfo!.CustomerName);
        Assert.Equal("Test sale", item.SampleRequestInfo.SaleName);
        Assert.Equal("COLOR_TEST", item.SampleRequestInfo.ColourCode);
        Assert.Equal("CMP", item.SampleRequestInfo.ProductCategory!.Code);
        Assert.Single(store.Root.Participants);
    }
}
