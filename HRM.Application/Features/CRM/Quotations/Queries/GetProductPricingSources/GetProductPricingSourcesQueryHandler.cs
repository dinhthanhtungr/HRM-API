using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingSources;

internal sealed class GetProductPricingSourcesQueryHandler
    : IRequestHandler<GetProductPricingSourcesQuery,
        OperationResult<IReadOnlyList<ProductPricingSourceOptionDto>>>
{
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ProductPricingSourceQueryService _sourceQueryService;

    public GetProductPricingSourcesQueryHandler(
        ICRMReadDbContext crmDbContext,
        ICurrentUser currentUser,
        ProductPricingSourceQueryService sourceQueryService)
    {
        _crmDbContext = crmDbContext;
        _currentUser = currentUser;
        _sourceQueryService = sourceQueryService;
    }

    public async Task<OperationResult<IReadOnlyList<ProductPricingSourceOptionDto>>> Handle(
        GetProductPricingSourcesQuery request,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(_currentUser))
        {
            return OperationResult<IReadOnlyList<ProductPricingSourceOptionDto>>.Fail(
                "Only President or Developer can view product pricing sources.");
        }

        if (request.ProductId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<ProductPricingSourceOptionDto>>.Fail(
                "ProductId and current company context are required.");
        }

        var productExists = await _crmDbContext.Products
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (!productExists)
        {
            return OperationResult<IReadOnlyList<ProductPricingSourceOptionDto>>.Fail(
                "Product was not found, inactive, or outside the current company.");
        }

        var sourcesByProduct = await _sourceQueryService.LoadAsync(
            [request.ProductId],
            companyId,
            includeSensitivePricing: true,
            cancellationToken);

        return OperationResult<IReadOnlyList<ProductPricingSourceOptionDto>>.Ok(
            sourcesByProduct.GetValueOrDefault(request.ProductId) ?? []);
    }
}
