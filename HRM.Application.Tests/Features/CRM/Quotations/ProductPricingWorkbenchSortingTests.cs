using HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbench;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingWorkbenchSortingTests
{
    [Fact]
    public void ApplySorting_Default_PrioritizesQuotationRequestsThenLatestSampleRequest()
    {
        var requestedProduct = Product("REQUESTED", new DateTime(2026, 8, 1));
        var newestSampleProduct = Product("NEWEST-SAMPLE", new DateTime(2026, 8, 20));
        var olderSampleProduct = Product("OLDER-SAMPLE", new DateTime(2026, 8, 10));
        var requests = new Dictionary<Guid, IReadOnlyList<ProductPricingRequestRow>>
        {
            [requestedProduct.ProductId] =
            [
                new ProductPricingRequestRow
                {
                    ProductId = requestedProduct.ProductId,
                    RequestedAt = new DateTime(2026, 7, 1)
                }
            ]
        };

        var result = GetProductPricingWorkbenchQueryHandler.ApplySorting(
            [olderSampleProduct, requestedProduct, newestSampleProduct],
            requests,
            new GetProductPricingWorkbenchQuery());

        Assert.Equal(
            [requestedProduct.ProductId, newestSampleProduct.ProductId, olderSampleProduct.ProductId],
            result.Select(x => x.ProductId));
    }

    [Fact]
    public void ApplySorting_Default_OrdersRequestedProductsByLatestRequestFirst()
    {
        var olderRequest = Product("OLDER-REQUEST", new DateTime(2026, 8, 20));
        var newerRequest = Product("NEWER-REQUEST", new DateTime(2026, 8, 1));
        var requests = new Dictionary<Guid, IReadOnlyList<ProductPricingRequestRow>>
        {
            [olderRequest.ProductId] =
            [
                new ProductPricingRequestRow
                {
                    ProductId = olderRequest.ProductId,
                    RequestedAt = new DateTime(2026, 8, 10)
                }
            ],
            [newerRequest.ProductId] =
            [
                new ProductPricingRequestRow
                {
                    ProductId = newerRequest.ProductId,
                    RequestedAt = new DateTime(2026, 8, 19)
                }
            ]
        };

        var result = GetProductPricingWorkbenchQueryHandler.ApplySorting(
            [olderRequest, newerRequest],
            requests,
            new GetProductPricingWorkbenchQuery());

        Assert.Equal(
            [newerRequest.ProductId, olderRequest.ProductId],
            result.Select(x => x.ProductId));
    }

    private static ProductRow Product(string code, DateTime sampleRequestCreatedDate)
        => new()
        {
            ProductId = Guid.NewGuid(),
            ProductCode = code,
            CreatedDate = new DateTime(2026, 1, 1),
            LatestSampleRequestCreatedDate = sampleRequestCreatedDate
        };
}
