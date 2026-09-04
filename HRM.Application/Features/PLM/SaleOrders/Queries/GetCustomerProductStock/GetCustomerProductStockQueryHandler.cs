using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetCustomerProductStock;

internal sealed class GetCustomerProductStockQueryHandler
    : IRequestHandler<GetCustomerProductStockQuery, OperationResult<CustomerProductStockSummaryDto>>
{
    private readonly ISaleOrderDbContext _saleOrderDbContext;
    private readonly IWarehouseReadDbContext _warehouseDbContext;
    private readonly ICustomerVisibilityService _customerVisibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCustomerProductStockQueryHandler(
        ISaleOrderDbContext saleOrderDbContext,
        IWarehouseReadDbContext warehouseDbContext,
        ICustomerVisibilityService customerVisibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _saleOrderDbContext = saleOrderDbContext;
        _warehouseDbContext = warehouseDbContext;
        _customerVisibilityService = customerVisibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<CustomerProductStockSummaryDto>> Handle(
        GetCustomerProductStockQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty || request.ProductId == Guid.Empty)
        {
            return OperationResult<CustomerProductStockSummaryDto>.Fail(
                "CustomerId hoặc ProductId không hợp lệ.");
        }

        var scope = await _customerVisibilityService.BuildScopeAsync(cancellationToken);
        var canAccessCustomer = await _customerVisibilityService
            .ApplyCustomerVisibility(
                _saleOrderDbContext.Customers.AsNoTracking(),
                scope)
            .AnyAsync(x => x.CustomerId == request.CustomerId, cancellationToken);
        if (!canAccessCustomer)
        {
            return OperationResult<CustomerProductStockSummaryDto>.Fail(
                "Không tìm thấy khách hàng hoặc bạn không có quyền truy cập.");
        }

        var product = await _saleOrderDbContext.Products
            .AsNoTracking()
            .Where(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive)
            .Select(x => new { x.ProductId, ProductCode = x.ColourCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null || string.IsNullOrWhiteSpace(product.ProductCode))
        {
            return OperationResult<CustomerProductStockSummaryDto>.Fail(
                "Không tìm thấy sản phẩm active hoặc sản phẩm chưa có mã tồn kho.");
        }

        var formulaSources = await _saleOrderDbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(mfg =>
                mfg.CompanyId == scope.CompanyId &&
                mfg.CustomerId == request.CustomerId &&
                mfg.ProductId == request.ProductId &&
                mfg.IsActive)
            .SelectMany(
                mfg => mfg.ProductionSelectVersions.Where(version =>
                    version.CompanyId == scope.CompanyId &&
                    version.ValidFrom.HasValue &&
                    version.ManufacturingFormulaId.HasValue &&
                    version.ManufacturingFormula != null &&
                    version.ManufacturingFormula.CompanyId == scope.CompanyId &&
                    version.ManufacturingFormula.ExternalId != string.Empty),
                (mfg, version) => new CustomerProductFormulaSource(
                    version.ManufacturingFormulaId!.Value,
                    version.ManufacturingFormula!.ExternalId,
                    mfg.MfgProductionOrderId,
                    mfg.ExternalId,
                    mfg.CreatedDate))
            .ToListAsync(cancellationToken);

        var formulaIds = formulaSources
            .Select(x => x.ManufacturingFormulaId)
            .Distinct()
            .ToArray();
        var formulaCustomerCounts = formulaIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await _saleOrderDbContext.MfgProductionOrders
                .AsNoTracking()
                .Where(mfg =>
                    mfg.CompanyId == scope.CompanyId &&
                    mfg.ProductId == request.ProductId &&
                    mfg.CustomerId.HasValue &&
                    mfg.IsActive)
                .SelectMany(
                    mfg => mfg.ProductionSelectVersions.Where(version =>
                        version.CompanyId == scope.CompanyId &&
                        version.ValidFrom.HasValue &&
                        version.ManufacturingFormulaId.HasValue &&
                        formulaIds.Contains(version.ManufacturingFormulaId.Value)),
                    (mfg, version) => new
                    {
                        ManufacturingFormulaId = version.ManufacturingFormulaId!.Value,
                        CustomerId = mfg.CustomerId!.Value
                    })
                .Distinct()
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.ManufacturingFormulaId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.CustomerId).Distinct().Count());

        var productStock = await WarehouseStockQueryHelper
            .ForItemStock(
                _warehouseDbContext.WarehouseShelfStocks.AsNoTracking(),
                scope.CompanyId,
                product.ProductCode,
                StockType.FinishedGood,
                excludeMixingShelf: true)
            .Where(x =>
                ((x.LotNo != null && x.LotNo != string.Empty) ||
                 (x.LotKey != null && x.LotKey != string.Empty)))
            .Select(x => new CustomerProductShelfStock(
                x.ShelfStockId,
                WarehouseStockQueryHelper.GetShelfDisplayName(x.ShelfStockCode),
                x.LotNo,
                x.LotKey,
                x.QtyKg))
            .ToListAsync(cancellationToken);

        var productReservations = await _warehouseDbContext.WarehouseTempStocks
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.Code == product.ProductCode &&
                x.ReserveStatus == ReserveStatus.Open.ToString() &&
                (x.QtyRequest ?? 0m) - (x.QtyUsed ?? 0m) > 0m)
            .Select(x => new CustomerProductReservation(
                x.LotKey,
                (x.QtyRequest ?? 0m) - (x.QtyUsed ?? 0m)))
            .ToListAsync(cancellationToken);

        var calculation = CustomerProductStockCalculator.Calculate(
            formulaSources,
            formulaCustomerCounts,
            productStock,
            productReservations);
        var result = new CustomerProductStockSummaryDto
        {
            CustomerId = request.CustomerId,
            ProductId = product.ProductId,
            ProductCode = product.ProductCode.Trim(),
            AsOf = _dateTimeProvider.Now,
            TotalOnHandKg = calculation.TotalOnHandKg,
            ReservedOpenKg = calculation.ReservedOpenKg,
            AvailableKg = calculation.AvailableKg,
            ProductAvailableKg = calculation.ProductAvailableKg,
            AmbiguousOnHandKg = calculation.AmbiguousOnHandKg,
            HasAmbiguousAttribution = calculation.Lots.Any(x => x.IsAttributionAmbiguous),
            Lots = calculation.Lots.Select(lot => new CustomerProductStockLotDto
            {
                ManufacturingFormulaId = lot.ManufacturingFormulaId,
                LotNo = lot.LotNo,
                OnHandKg = lot.OnHandKg,
                ReservedOpenKg = lot.ReservedOpenKg,
                AvailableKg = lot.AvailableKg,
                IsAttributionAmbiguous = lot.IsAttributionAmbiguous,
                SourceMfgOrders = lot.SourceMfgOrders.Select(mfg => new CustomerProductStockMfgDto
                {
                    MfgProductionOrderId = mfg.MfgProductionOrderId,
                    ExternalId = mfg.ExternalId
                }).ToList(),
                Shelves = lot.Shelves.Select(shelf => new CustomerProductStockShelfDto
                {
                    ShelfCode = shelf.ShelfCode,
                    OnHandKg = shelf.OnHandKg
                }).ToList()
            }).ToList()
        };

        return OperationResult<CustomerProductStockSummaryDto>.Ok(result);
    }
}
