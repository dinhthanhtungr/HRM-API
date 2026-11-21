using HRM.Application.Features.InternalMail.Queries.GetRelatedProducts;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("unlinked", 2)]
    [InlineData("explicit", 1)]
    [InlineData("missing-explicit", 0)]
    [InlineData("revoked-explicit", 0)]
    [InlineData("mixed-lines", 2)]
    public async Task RelatedProductsFallbackUsesExactProductAndAllVisibleGeneralRequestsOnlyWhenLinkAbsent(
        string linkMode, int expectedTargets)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        var company = user.CompanyId!.Value;
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company, ColourCode = "SAME_CODE" };
        var samples = Enumerable.Range(0, 6).Select(i => new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), ProductId = product.ProductId,
            CompanyId = company, IsActive = true, ExternalId = $"TP_{i}", Product = product
        }).ToArray();
        samples[3].CompanyId = Guid.NewGuid();
        samples[4].IsActive = false;
        // Mã sản phẩm giống nhau không phải là liên kết: chỉ ProductId mới có ý nghĩa.
        samples[5].ProductId = Guid.NewGuid();
        samples[5].Product = new Product { ProductId = samples[5].ProductId, CompanyId = company, ColourCode = "SAME_CODE" };
        var targets = samples.Select((s, i) => new InternalConversation
        {
            InternalConversationId = Guid.NewGuid(), RelatedType = InternalMailRelatedType.SampleRequest,
            RelatedId = s.SampleRequestId, CompanyId = company, IsActive = true,
            Participants = [new() { EmployeeId = user.EmployeeId!.Value, IsActive = i != 2 }]
        }).ToArray();
        var quote = new Quotation
        {
            QuotationId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Lines = [new() { QuotationLineId = Guid.NewGuid(), ProductId = product.ProductId,
                ProductNavigation = product, IsActive = true, ProductExternalIdSnapshot = "SAME_CODE",
                SampleRequestId = linkMode switch
                {
                    "explicit" => samples[0].SampleRequestId,
                    "missing-explicit" => Guid.NewGuid(),
                    "revoked-explicit" => samples[2].SampleRequestId,
                    _ => null
                } }]
        };
        if (linkMode == "mixed-lines") quote.Lines.Add(new QuotationLine
        {
            QuotationLineId = Guid.NewGuid(), ProductId = product.ProductId, ProductNavigation = product,
            IsActive = true, SampleRequestId = samples[0].SampleRequestId
        });
        store.Child.RelatedId = quote.QuotationId;
        var proxy = (MemoryStore)(object)store.Db;
        proxy.Values["Quotations"] = new MemorySet<Quotation>([quote]);
        proxy.Values["SampleRequests"] = new MemorySet<SampleRequest>(samples);
        proxy.Values["InternalConversations"] = new MemorySet<InternalConversation>([store.Root, store.Child, .. targets]);
        var result = await new GetInternalConversationRelatedProductsQueryHandler(store.Db, user, new(store.Db, user))
            .Handle(new(store.Child.InternalConversationId), default);
        var actual = Assert.Single(result!).Conversations;
        Assert.Equal(expectedTargets, actual.Count);
        if (linkMode is "unlinked" or "mixed-lines")
            Assert.Equal(samples.Take(2).Select(s => s.SampleRequestId), actual.Select(c => c.SampleRequestId));
        if (linkMode == "explicit") Assert.Equal(samples[0].SampleRequestId, Assert.Single(actual).SampleRequestId);
    }

    [Fact]
    public async Task RelatedProductsDeduplicateLinesAndReturnOnlyExplicitVisibleGeneralTargets()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        var company = user.CompanyId!.Value;
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company };
        var second = new Product { ProductId = Guid.NewGuid(), CompanyId = company };
        var samples = Enumerable.Range(0, 3).Select(i => new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), ProductId = product.ProductId,
            CompanyId = company, IsActive = true, ExternalId = $"TP_{i}", Product = product
        }).ToArray();
        var targets = samples.Select(s => new InternalConversation
        {
            InternalConversationId = Guid.NewGuid(), RelatedType = InternalMailRelatedType.SampleRequest,
            RelatedId = s.SampleRequestId, CompanyId = company, IsActive = true,
            Participants = [new() { EmployeeId = user.EmployeeId!.Value, IsActive = true }]
        }).ToArray();
        var quotation = new Quotation
        {
            QuotationId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Lines = [Line(product, samples[0], 2), Line(product, samples[0], 3), Line(product, samples[1], 4),
                Line(second, null, 1)]
        };
        store.Child.RelatedId = quotation.QuotationId;
        var proxy = (MemoryStore)(object)store.Db;
        proxy.Values["Quotations"] = new MemorySet<Quotation>([quotation]);
        proxy.Values["SampleRequests"] = new MemorySet<SampleRequest>(samples);
        proxy.Values["InternalConversations"] = new MemorySet<InternalConversation>([store.Root, store.Child, .. targets]);
        var handler = new GetInternalConversationRelatedProductsQueryHandler(store.Db, user, new(store.Db, user));
        var result = await handler.Handle(new(store.Child.InternalConversationId), default);
        Assert.Equal(new[] { second.ProductId, product.ProductId }, result!.Select(p => p.ProductId));
        Assert.Empty(result[0].Conversations);
        Assert.Equal(new[] { samples[0].SampleRequestId, samples[1].SampleRequestId },
            result[1].Conversations.Select(c => c.SampleRequestId));
        Assert.DoesNotContain(result[1].Conversations, c => c.SampleRequestId == samples[2].SampleRequestId);
        Assert.All(result, p => Assert.Equal("snapshot-name", p.ProductName));
        Assert.Single(store.Child.Participants);

        QuotationLine Line(Product p, SampleRequest? sample, int order) => new()
        {
            QuotationLineId = Guid.NewGuid(), ProductId = p.ProductId, ProductNavigation = p,
            SampleRequestId = sample?.SampleRequestId, SortOrder = order, IsActive = true,
            ProductExternalIdSnapshot = "snapshot-code", ProductNameSnapshot = "snapshot-name"
        };
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("foreign-sample")]
    [InlineData("inactive-sample")]
    [InlineData("wrong-product")]
    [InlineData("private-group")]
    [InlineData("foreign-target")]
    public async Task RelatedProductsNeverExposeUnauthorizedOrMismatchedTarget(string boundary)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        var company = user.CompanyId!.Value;
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company };
        var sample = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), ProductId = product.ProductId, CompanyId = company,
            IsActive = true, ExternalId = "TP_TEST", Product = product
        };
        var target = new InternalConversation
        {
            InternalConversationId = Guid.NewGuid(), RelatedType = InternalMailRelatedType.SampleRequest,
            RelatedId = sample.SampleRequestId, CompanyId = company, IsActive = true,
            Participants = [new() { EmployeeId = user.EmployeeId!.Value, IsActive = boundary != "revoked" }]
        };
        if (boundary == "foreign-sample") sample.CompanyId = Guid.NewGuid();
        if (boundary == "inactive-sample") sample.IsActive = false;
        if (boundary == "wrong-product") sample.ProductId = Guid.NewGuid();
        if (boundary == "private-group") target.RelatedType = InternalMailRelatedType.ConversationPricing;
        if (boundary == "foreign-target") target.CompanyId = Guid.NewGuid();
        var quote = new Quotation
        {
            QuotationId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Lines = [new() { QuotationLineId = Guid.NewGuid(), ProductId = product.ProductId,
                ProductNavigation = product, SampleRequestId = sample.SampleRequestId, IsActive = true }]
        };
        store.Child.RelatedId = quote.QuotationId;
        var proxy = (MemoryStore)(object)store.Db;
        proxy.Values["Quotations"] = new MemorySet<Quotation>([quote]);
        proxy.Values["SampleRequests"] = new MemorySet<SampleRequest>([sample]);
        proxy.Values["InternalConversations"] = new MemorySet<InternalConversation>([store.Root, store.Child, target]);
        var result = await new GetInternalConversationRelatedProductsQueryHandler(store.Db, user, new(store.Db, user))
            .Handle(new(store.Child.InternalConversationId), default);
        Assert.Empty(Assert.Single(result!).Conversations);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("wrong-type")]
    [InlineData("anonymous")]
    public async Task RelatedProductsRejectInaccessibleQuotationSource(string boundary)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        if (boundary == "revoked") store.Child.Participants.Clear();
        if (boundary == "foreign") store.Child.CompanyId = Guid.NewGuid();
        if (boundary == "inactive") store.Child.IsActive = false;
        if (boundary == "wrong-type") store.Child.RelatedType = InternalMailRelatedType.SampleRequest;
        if (boundary == "anonymous") user.IsAuthenticated = false;
        var result = await new GetInternalConversationRelatedProductsQueryHandler(store.Db, user, new(store.Db, user))
            .Handle(new(store.Child.InternalConversationId), default);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("foreign-quotation", true)]
    [InlineData("inactive-quotation", true)]
    [InlineData("foreign-product", false)]
    [InlineData("inactive-line", false)]
    public async Task RelatedProductsScopeQuotationAndLineRecords(string boundary, bool notFound)
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        var company = user.CompanyId!.Value;
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company };
        var line = new QuotationLine
        {
            QuotationLineId = Guid.NewGuid(), ProductId = product.ProductId,
            ProductNavigation = product, IsActive = true
        };
        var quote = new Quotation { QuotationId = Guid.NewGuid(), CompanyId = company, IsActive = true, Lines = [line] };
        store.Child.RelatedId = quote.QuotationId;
        if (boundary == "foreign-quotation") quote.CompanyId = Guid.NewGuid();
        if (boundary == "inactive-quotation") quote.IsActive = false;
        if (boundary == "foreign-product") product.CompanyId = Guid.NewGuid();
        if (boundary == "inactive-line") line.IsActive = false;
        ((MemoryStore)(object)store.Db).Values["Quotations"] = new MemorySet<Quotation>([quote]);
        var result = await new GetInternalConversationRelatedProductsQueryHandler(store.Db, user, new(store.Db, user))
            .Handle(new(store.Child.InternalConversationId), default);
        if (notFound) Assert.Null(result);
        else Assert.Empty(result!);
    }

    [Fact]
    public async Task RelatedProductsKeepHistoricalSnapshotWhenProductIsLaterInactive()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = false };
        var quote = new Quotation
        {
            QuotationId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true,
            Lines = [new() { QuotationLineId = Guid.NewGuid(), ProductId = product.ProductId,
                ProductNavigation = product, IsActive = true, ProductExternalIdSnapshot = "OLD_CODE", ProductNameSnapshot = "Historical name" }]
        };
        store.Child.RelatedId = quote.QuotationId;
        ((MemoryStore)(object)store.Db).Values["Quotations"] = new MemorySet<Quotation>([quote]);
        var result = await new GetInternalConversationRelatedProductsQueryHandler(store.Db, user, new(store.Db, user))
            .Handle(new(store.Child.InternalConversationId), default);
        var actual = Assert.Single(result!);
        Assert.Equal("OLD_CODE", actual.ProductCode);
        Assert.Equal("Historical name", actual.ProductName);
    }
}
