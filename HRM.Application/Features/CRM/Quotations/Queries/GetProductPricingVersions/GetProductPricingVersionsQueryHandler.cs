using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingVersions;

internal sealed class GetProductPricingVersionsQueryHandler
    : IRequestHandler<GetProductPricingVersionsQuery,
        OperationResult<IReadOnlyList<ProductPricingVersionDto>>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetProductPricingVersionsQueryHandler(
        ICRMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<IReadOnlyList<ProductPricingVersionDto>>> Handle(
        GetProductPricingVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(_currentUser))
            return OperationResult<IReadOnlyList<ProductPricingVersionDto>>.Fail(
                "Only President or Developer can view product pricing version history.");
        if (request.ProductId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<IReadOnlyList<ProductPricingVersionDto>>.Fail(
                "ProductId and current company context are required.");

        var currency = QuotationRules.TrimToNull(request.Currency)?.ToUpperInvariant();
        if (currency is { Length: > QuotationRules.MaximumCurrencyLength })
            return OperationResult<IReadOnlyList<ProductPricingVersionDto>>.Fail(
                $"Currency cannot exceed {QuotationRules.MaximumCurrencyLength} characters.");

        var query = _dbContext.ProductPricingVersions.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.FormulaPricingPolicy)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .Where(x => x.CompanyId == companyId && x.ProductId == request.ProductId && x.IsActive);
        if (currency is not null)
            query = query.Where(x => x.Currency == currency);

        var entities = await query.OrderByDescending(x => x.Version).ToListAsync(cancellationToken);
        return OperationResult<IReadOnlyList<ProductPricingVersionDto>>.Ok(
            entities.Select(x => ProductPricingVersionMapper.ToDto(x)).ToArray());
    }
}
