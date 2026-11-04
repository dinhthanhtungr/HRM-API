using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(false, "Developer", false, "ok", true)]
    [InlineData(true, "Developer", false, "ok", true)]
    [InlineData(false, "SaleUser", true, "ok", true)]
    [InlineData(true, "SaleUser", true, "ok", true)]
    [InlineData(false, "SaleUser", false, "ok", false)]
    [InlineData(true, "SaleUser", false, "ok", false)]
    [InlineData(false, "Developer", false, "foreign-quotation", false)]
    [InlineData(false, "Developer", false, "foreign-customer", false)]
    [InlineData(true, "Developer", false, "foreign-sale", false)]
    [InlineData(true, "Developer", false, "inactive-quotation", false)]
    [InlineData(true, "Developer", false, "inactive-parent", false)]
    public async Task QuotationFeedMetadataStaysScopedForDirectAndPrivateConversations(
        bool privateGroup, string role, bool member, string boundary, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, privateGroup ? InternalMailRelatedType.ConversationCustom : InternalMailRelatedType.Quotation, member);
        var companyId = user.CompanyId!.Value;
        var quotation = new Quotation
        {
            QuotationId = Guid.NewGuid(), CompanyId = companyId, ExternalId = "BBG260900316", IsActive = true,
            Customer = new() { CompanyId = companyId, CustomerName = "Quotation Customer", ExternalId = "KH_1" },
            SaleEmployee = new() { CompanyId = companyId, FullName = "Quotation Sale" }
        };
        if (privateGroup)
        {
            store.Root.RelatedType = InternalMailRelatedType.Quotation;
            store.Root.RelatedId = quotation.QuotationId;
        }
        else store.Child.RelatedId = quotation.QuotationId;
        switch (boundary)
        {
            case "foreign-quotation": quotation.CompanyId = Guid.NewGuid(); break;
            case "foreign-customer": quotation.Customer.CompanyId = Guid.NewGuid(); break;
            case "foreign-sale": quotation.SaleEmployee.CompanyId = Guid.NewGuid(); break;
            case "inactive-quotation": quotation.IsActive = false; break;
            case "inactive-parent": store.Root.IsActive = false; break;
        }
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.Quotations)] = new MemorySet<Quotation>([quotation]);
        var resolver = new NotificationFeedConversationInfoResolver(store.Db, new InternalMailAreaAccessService(store.Db, user),
            new InternalConversationSampleRequestInfoResolver(store.Db), new InternalConversationQuotationInfoResolver(store.Db));
        var result = await resolver.ResolveAsync([store.Child.InternalConversationId, store.Child.InternalConversationId], companyId, default);
        Assert.Empty(result.SampleRequests);
        Assert.Equal(expected ? 1 : 0, result.Quotations.Count);
        if (expected)
        {
            var info = Assert.Single(result.Quotations).Value;
            Assert.Equal("BBG260900316", info.QuotationCode);
            Assert.Equal("Quotation Customer", info.CustomerName);
            Assert.Equal("Quotation Sale", info.SaleName);
        }
    }
}
