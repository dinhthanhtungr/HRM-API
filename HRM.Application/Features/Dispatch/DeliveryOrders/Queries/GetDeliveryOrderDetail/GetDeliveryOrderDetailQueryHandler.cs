using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Dispatch.Deliverers.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrderDetail;

internal sealed class GetDeliveryOrderDetailQueryHandler
    : IRequestHandler<GetDeliveryOrderDetailQuery, DeliveryOrderDetailDto?>
{
    private readonly IDispatchReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissionService _permissionService;

    public GetDeliveryOrderDetailQueryHandler(
        IDispatchReadDbContext dbContext,
        ICurrentUser currentUser,
        ICurrentUserPermissionService permissionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<DeliveryOrderDetailDto?> Handle(
        GetDeliveryOrderDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty ||
            !DeliveryOrderAccessRules.CanRead(_currentUser) ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        var canViewCost = DeliveryOrderCostVisibilityRules.CanViewCost(_permissionService);
        var canManage = DeliveryOrderAccessRules.CanManage(_currentUser);

        // DELETE cũ tắt cả dòng/lot và không lưu mốc vô hiệu hóa. Chỉ phục hồi trường hợp
        // có duy nhất một dòng; không đoán snapshot cuối từ nhiều phiên bản lịch sử.
        var recoverSingleDeletedLine = await _dbContext.DeliveryOrders.AsNoTracking()
            .AnyAsync(x => x.Id == request.Id && x.CompanyId == companyId && !x.IsActive &&
                (x.Status == "Cancelled" || x.Status == "Canceled") &&
                x.Details.Count() == 1 && !x.Details.Any(d => d.IsActive), cancellationToken);

        var labelNames = DeliveryCustomerLabelNames.Query(_dbContext.CustomerLabelDetails.AsNoTracking(), companyId);
        var result = await _dbContext.DeliveryOrders
            .AsNoTracking()
            .Where(x => x.Id == request.Id && x.CompanyId == companyId)
            .Select(x => new DeliveryOrderDetailDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                Status = x.Status == "Cancelled" ? "Canceled" : x.Status,
                CompanyId = x.CompanyId,
                CustomerId = x.CustomerId,
                CustomerExternalIdSnapshot = x.CustomerExternalIdSnapShot,
                CustomerName = x.Customer.CustomerName,
                MerchandiseOrderExternalIds = string.Join(", ",
                    x.DeliveryOrderPOs
                        .Where(po => po.IsActive)
                        .Select(po => po.MerchandiseOrder.ExternalId)
                        .Where(value => value != null && value != string.Empty)
                        .Distinct()),
                Receiver = x.Receiver,
                DeliveryAddress = x.DeliveryAddress,
                PaymentType = x.PaymentType,
                PaymentDeadline = x.PaymentDeadline,
                TaxNumber = x.TaxNumber,
                PhoneSnapshot = x.PhoneSnapshot,
                RequiresUnloading = x.RequiresUnloading,
                DeliveryPrice = x.DeliveryPrice,
                Note = x.Note,
                IsActive = x.IsActive,
                HasPrinted = x.HasPrinted,
                CreatedBy = x.CreatedBy,
                CreatedDate = x.CreatedDate,
                UpdatedBy = x.UpdatedBy,
                UpdatedDate = x.UpdatedDate,
                CanEdit = canManage && x.IsActive && x.Status == "Pending" && !x.HasPrinted,
                LineCount = x.Details.Count(d => (d.IsActive || recoverSingleDeletedLine) && !d.IsAttach),
                TotalQuantity = x.Details
                    .Where(d => (d.IsActive || recoverSingleDeletedLine) && !d.IsAttach)
                    .Sum(d => d.Quantity),
                TotalNumOfBags = x.Details
                    .Where(d => (d.IsActive || recoverSingleDeletedLine) && !d.IsAttach)
                    .Sum(d => d.NumOfBags),
                Lines = x.Details
                    .Where(d => d.IsActive || recoverSingleDeletedLine)
                    .OrderBy(d => d.PONo)
                    .ThenBy(d => d.ProductExternalIdSnapShot)
                    .Select(d => new DeliveryOrderLineDto
                    {
                        Id = d.Id,
                        MerchandiseOrderDetailId = d.MerchandiseOrderDetailId,
                        ProductId = d.ProductId,
                        ProductExternalId = d.ProductExternalIdSnapShot,
                        ProductName = d.ProductNameSnapShot,
                        ProductDisplayCode = d.Product != null && d.Product.CompanyId == companyId ? d.Product.ColourCode : null,
                        ProductDisplayName = labelNames.Where(n => n.Header.CustomerId == x.CustomerId && n.Header.ProductId == d.ProductId)
                            .Select(n => n.FieldValue!.Trim()).FirstOrDefault() ??
                            (d.Product != null && d.Product.CompanyId == companyId ? d.Product.Name : null),
                        BagWeightKg = d.Product != null && d.Product.IsActive && d.Product.CompanyId == companyId
                            ? d.Product.SampleRequests.Where(sr => sr.CompanyId == companyId && sr.IsActive && sr.CustomerId == x.CustomerId)
                                .OrderByDescending(sr => sr.CreatedDate).ThenByDescending(sr => sr.SampleRequestId)
                                .Select(sr => sr.BagWeight > 0 ? (int?)sr.BagWeight : null).FirstOrDefault() : null,
                        LotNoList = d.LotConsumptions.Any(lot => lot.IsActive || recoverSingleDeletedLine)
                            ? string.Join(", ", d.LotConsumptions
                                .Where(lot => lot.IsActive || recoverSingleDeletedLine)
                                .OrderBy(lot => lot.LotNo)
                                .Select(lot => lot.LotNo))
                            : d.LotNoList,
                        Lots = d.LotConsumptions
                            .Where(lot => lot.IsActive || recoverSingleDeletedLine)
                            .OrderBy(lot => lot.LotNo)
                            .Select(lot => new DeliveryOrderLotDto
                            {
                                LotNo = lot.LotNo,
                                Quantity = lot.Quantity,
                                UnitCostSnapshot = canViewCost ? lot.UnitCostSnapshot : null,
                                TotalCostSnapshot = canViewCost ? lot.TotalCostSnapshot : null
                            })
                            .ToList(),
                        PONo = d.PONo,
                        Quantity = d.Quantity,
                        NumOfBags = d.NumOfBags,
                        IsAttach = d.IsAttach
                    })
                    .ToList(),
                Deliverers = x.Deliverers
                    .OrderBy(d => d.DelivererInfor.Name)
                    .Select(d => new DelivererDto
                    {
                        Id = d.DelivererInforId,
                        Name = d.DelivererInfor.Name,
                        DelivererType = d.DelivererInfor.DelivererType,
                        Phone = d.DelivererInfor.Phone,
                        Note = d.DelivererInfor.Note,
                        IsActive = d.DelivererInfor.IsActive
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (result is null) return null;
        var requests = await _dbContext.WarehouseRequests.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && x.codeFromRequest == result.ExternalId &&
                x.ReqType == WareHouseRequestType.ExportFinishedGood)
            .Select(x => new { x.RequestId, Details = x.WarehouseRequestDetails.Where(d => d.IsActive)
                .Select(d => new { d.ProductCode, d.LotNumber, d.ItemStockType, d.WeightKg, d.BagNumber }).ToList() })
            .ToListAsync(cancellationToken);
        // Không đoán loại tồn khi PRQ legacy có nhiều lot trong một chuỗi hoặc nhiều PRQ trùng mã.
        if (requests.Count == 1)
            result.StockAllocations = requests[0].Details
                .Where(x => (x.ItemStockType is null || x.ItemStockType == StockType.FinishedGood || x.ItemStockType == StockType.DefectiveFinishedGood) &&
                    !string.IsNullOrWhiteSpace(x.LotNumber) && !x.LotNumber.Any(c => ",;|\n\r\t".Contains(c)))
                .GroupBy(x => new { Code = DeliveryOrderLotInventoryRules.Normalize(x.ProductCode),
                    Lot = DeliveryOrderLotInventoryRules.Normalize(x.LotNumber!), Type = x.ItemStockType ?? StockType.FinishedGood })
                .Select(x => new DeliveryStockAllocationDto { ProductCode = x.First().ProductCode,
                    LotNo = x.First().LotNumber!, StockType = x.Key.Type.ToString(), Quantity = x.Sum(d => d.WeightKg), NumOfBags = x.Sum(d => d.BagNumber) }).ToArray();
        var snapshotTotals = result.Lines.Where(x => !x.IsAttach).SelectMany(line => line.Lots.Select(lot => new
            { Key = $"{DeliveryOrderLotInventoryRules.Normalize(line.ProductExternalId ?? string.Empty)}|{DeliveryOrderLotInventoryRules.Normalize(lot.LotNo)}", lot.Quantity }))
            .GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(lot => lot.Quantity));
        var stockTotals = result.StockAllocations.GroupBy(x =>
            $"{DeliveryOrderLotInventoryRules.Normalize(x.ProductCode)}|{DeliveryOrderLotInventoryRules.Normalize(x.LotNo)}")
            .ToDictionary(x => x.Key, x => x.Sum(lot => lot.Quantity));
        result.StockAllocationsComplete = requests.Count == 1 && snapshotTotals.Count > 0 &&
            snapshotTotals.All(x => stockTotals.TryGetValue(x.Key, out var quantity) && quantity == x.Value) &&
            result.Lines.Where(x => !x.IsAttach).All(x => x.Lots.Sum(lot => lot.Quantity) == x.Quantity);
        return result;
    }
}
