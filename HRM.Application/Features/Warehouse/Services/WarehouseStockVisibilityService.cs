using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Services;

internal sealed class WarehouseStockVisibilityService : IWarehouseStockVisibilityService
{
    private readonly IWarehouseReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _customerVisibilityService;

    public WarehouseStockVisibilityService(
        IWarehouseReadDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService customerVisibilityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _customerVisibilityService = customerVisibilityService;
    }

    public async Task<IQueryable<WarehouseShelfStock>> ApplyAsync(
        IQueryable<WarehouseShelfStock> stockQuery,
        Guid companyId,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        if (!IsSalesScopedUser())
        {
            return stockQuery;
        }

        var (visibleProductCodes, visibleMaterialCodes) =
            await BuildAllowedCodesAsync(companyId, keyword, cancellationToken);

        return stockQuery.Where(stock =>
            ((stock.StockType == StockType.RawMaterial ||
              stock.StockType == StockType.DefectiveRawMaterial) &&
             visibleMaterialCodes.Contains(stock.Code!)) ||
            ((stock.StockType == StockType.FinishedGood ||
              stock.StockType == StockType.DefectiveFinishedGood) &&
             visibleProductCodes.Contains(stock.Code!)));
    }

    public async Task<IQueryable<WarehouseVoucherDetail>> ApplyVoucherDetailsAsync(
        IQueryable<WarehouseVoucherDetail> detailQuery,
        Guid companyId,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        if (!IsSalesScopedUser())
        {
            return detailQuery;
        }

        var (visibleProductCodes, visibleMaterialCodes) =
            await BuildAllowedCodesAsync(companyId, keyword, cancellationToken);

        return detailQuery.Where(detail =>
            visibleProductCodes.Contains(detail.ProductCode) ||
            visibleMaterialCodes.Contains(detail.ProductCode));
    }

    private async Task<(IQueryable<string> ProductCodes, IQueryable<string> MaterialCodes)> BuildAllowedCodesAsync(
        Guid companyId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var scope = await _customerVisibilityService.BuildScopeAsync(cancellationToken);
        // KH_VIETAUS luôn thuộc phạm vi xem tồn kho/lịch sử của Sale.
        // Keyword chỉ còn dùng cho tìm kiếm, không còn là điều kiện mở quyền.
        var effectiveScope = scope with { CanViewInternalCustomer = true };
        var visibleSampleRequests = _customerVisibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            effectiveScope);

        var visibleProductIds = visibleSampleRequests.Select(x => x.ProductId).Distinct();
        var visibleFormulaIds = visibleSampleRequests
            .Where(x => x.FormulaId.HasValue)
            .Select(x => x.FormulaId!.Value)
            .Distinct();
        var formulaMaterials = _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => x.IsActive && visibleFormulaIds.Contains(x.FormulaId));
        var visibleProductIdsIncludingComponents = visibleProductIds
            .Concat(formulaMaterials.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value))
            .Distinct();
        var visibleProductCodes = _dbContext.Products
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId &&
                        x.ColourCode != null &&
                        x.ColourCode != string.Empty &&
                        visibleProductIdsIncludingComponents.Contains(x.ProductId))
            .Select(x => x.ColourCode!);
        var visibleMaterialCodes =
            from formulaMaterial in formulaMaterials
            join material in _dbContext.Materials.AsNoTracking()
                on formulaMaterial.MaterialId equals material.MaterialId
            where material.CompanyId == companyId &&
                  material.ExternalId != null &&
                  material.ExternalId != string.Empty
            select material.ExternalId!;

        return (visibleProductCodes, visibleMaterialCodes);
    }

    private bool IsSalesScopedUser()
    {
        var isSale = _currentUser.IsInRole(ApplicationRoles.Sales.SaleUser) ||
                     _currentUser.IsInRole(ApplicationRoles.Sales.SaleAdmin);
        return isSale &&
               !_currentUser.IsInRole(ApplicationRoles.Admin) &&
               !_currentUser.IsInRole(ApplicationRoles.Developer) &&
               !_currentUser.IsInRole(ApplicationRoles.President) &&
               !_currentUser.IsInRole(ApplicationRoles.Warehouse.KHOUser) &&
               !_currentUser.IsInRole(ApplicationRoles.Sales.CustomerViewAll);
    }

}
