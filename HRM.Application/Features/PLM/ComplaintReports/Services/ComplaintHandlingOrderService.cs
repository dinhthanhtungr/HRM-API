using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

/// <summary>
/// Creates or synchronizes the zero-value SaleOrder used for complaint replacement production.
/// Approval and MFG creation are owned by SaleOrderApprovalService in the caller transaction.
/// </summary>
internal sealed class ComplaintHandlingOrderService
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly IExternalIdService _externalIdService;
    private readonly IEventLogWriter _eventLogWriter;

    public ComplaintHandlingOrderService(
        IComplaintReportDbContext dbContext,
        IExternalIdService externalIdService,
        IEventLogWriter eventLogWriter)
    {
        _dbContext = dbContext;
        _externalIdService = externalIdService;
        _eventLogWriter = eventLogWriter;
    }

    public async Task<OperationResult<MerchandiseOrder>> CreateAsync(
        ComplaintReport report,
        Guid employeeId,
        DateTime now,
        DateTime? requestedDeliveryDate,
        CancellationToken cancellationToken)
    {
        if (report.ResolutionType != ComplaintResolutionType.ReplacementProduction ||
            report.Status is ComplaintReportStatus.Draft or ComplaintReportStatus.Submitted
                or ComplaintReportStatus.Rejected or ComplaintReportStatus.Cancelled)
        {
            return OperationResult<MerchandiseOrder>.Fail(
                "Complaint chưa được initial approval cho hướng sản xuất bù.");
        }

        var lineIds = report.ComplaintReportLines.Where(x => x.IsActive)
            .Select(x => x.ComplaintReportLineId).ToArray();
        if (lineIds.Length == 0)
        {
            return OperationResult<MerchandiseOrder>.Fail("Complaint không có dòng active để sản xuất bù.");
        }

        var lines = await _dbContext.ComplaintReportLines.AsNoTracking()
            .Where(x => lineIds.Contains(x.ComplaintReportLineId) && x.IsActive)
            .Select(x => new ReplacementLine(
                x.ComplaintReportLineId,
                x.ProductId,
                x.ProductExternalIdSnapshot,
                x.ProductNameSnapshot,
                x.FormulaId,
                x.FormulaExternalIdSnapshot,
                x.ApprovedReplacementQuantity,
                x.ComplaintQuantity,
                x.Description,
                x.SourceMerchandiseOrderDetail.BagType,
                x.SourceMerchandiseOrderDetail.PackageWeight,
                x.SourceMerchandiseOrderDetail.BaseCostSnapshot,
                x.SourceMerchandiseOrderDetail.MerchandiseOrder))
            .ToListAsync(cancellationToken);
        if (lines.Count != lineIds.Length || lines.Any(x =>
            x.SourceOrder.CompanyId != report.CompanyId || x.SourceOrder.CustomerId != report.CustomerId ||
            !x.ApprovedQuantity.HasValue || x.ApprovedQuantity <= 0 || x.ApprovedQuantity > x.ComplaintQuantity))
        {
            return OperationResult<MerchandiseOrder>.Fail("Dòng complaint hoặc số lượng sản xuất bù không còn hợp lệ.");
        }

        var existingOrder = await _dbContext.MerchandiseOrders
            .Include(x => x.MerchandiseOrderDetails)
            .FirstOrDefaultAsync(x => x.CompanyId == report.CompanyId &&
                x.ComplaintReportId == report.ComplaintReportId && x.IsActive, cancellationToken);
        if (existingOrder is not null)
        {
            return SynchronizeExisting(existingOrder, lines, employeeId, now, requestedDeliveryDate);
        }

        var customer = await _dbContext.Customers.AsNoTracking()
            .Where(x => x.CustomerId == report.CustomerId && x.CompanyId == report.CompanyId && x.IsActive != false)
            .Select(x => new
            {
                x.CustomerId,
                x.CustomerName,
                x.ExternalId,
                x.Phone,
                x.IsLead,
                x.CurrentSaleId,
                LatestAssignmentEmployeeId = x.CustomerAssignments
                    .Where(a => a.IsActive && a.CompanyId == report.CompanyId)
                    .OrderByDescending(a => a.CreatedDate)
                    .Select(a => (Guid?)a.EmployeeId)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return OperationResult<MerchandiseOrder>.Fail("Không tìm thấy khách hàng của complaint.");
        }

        var managerId = customer.IsLead
            ? employeeId
            : customer.LatestAssignmentEmployeeId ?? customer.CurrentSaleId ?? employeeId;
        var manager = await FindManagerAsync(managerId, report.CompanyId, cancellationToken);
        if (manager is null && managerId != employeeId)
        {
            manager = await FindManagerAsync(employeeId, report.CompanyId, cancellationToken);
        }
        if (manager is null)
        {
            return OperationResult<MerchandiseOrder>.Fail("Không tìm thấy sale phụ trách hợp lệ.");
        }

        var sourceHeader = lines.OrderByDescending(x => x.SourceOrder.CreateDate).First().SourceOrder;
        var orderId = Guid.CreateVersion7();
        var attachmentCollectionId = Guid.CreateVersion7();
        await _dbContext.AttachmentCollections.AddAsync(
            new AttachmentCollection { AttachmentCollectionId = attachmentCollectionId }, cancellationToken);
        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = orderId,
            ExternalId = await _externalIdService.GenerateMonthlyCodeAsync(
                report.CompanyId, DocumentPrefix.DHG.ToString(), cancellationToken),
            OrderType = OrderType.Complaint,
            ComplaintReportId = report.ComplaintReportId,
            AttachmentCollectionId = attachmentCollectionId,
            CustomerId = customer.CustomerId,
            CustomerNameSnapshot = customer.CustomerName,
            CustomerExternalIdSnapshot = customer.ExternalId,
            PhoneSnapshot = customer.Phone ?? sourceHeader.PhoneSnapshot,
            ManagerById = manager.EmployeeId,
            ManagerByNameSnapshot = manager.FullName,
            ManagerExternalIdSnapshot = manager.ExternalId,
            Receiver = sourceHeader.Receiver,
            DeliveryAddress = sourceHeader.DeliveryAddress,
            TotalPrice = 0,
            PaymentType = sourceHeader.PaymentType,
            Vat = 0,
            Status = MerchadiseStatus.New.ToString(),
            Currency = sourceHeader.Currency,
            ExchangeRate = sourceHeader.ExchangeRate,
            CompanyId = report.CompanyId,
            IsPaid = true,
            IsActive = true,
            PaymentDate = now,
            Note = $"Đơn xử lý khiếu nại {report.ExternalId}",
            ShippingMethod = sourceHeader.ShippingMethod,
            PONo = string.Empty,
            CreateDate = now,
            CreatedBy = employeeId,
            UpdatedDate = now,
            UpdatedBy = employeeId
        };
        foreach (var line in lines)
        {
            order.MerchandiseOrderDetails.Add(CreateDetail(
                orderId, line, employeeId, now,
                requestedDeliveryDate ?? report.RequestedReplacementDeliveryDate ?? now));
        }

        await _dbContext.MerchandiseOrders.AddAsync(order, cancellationToken);
        await _eventLogWriter.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId,
            CompanyId = report.CompanyId,
            SourceType = nameof(MerchandiseOrder),
            SourceId = order.MerchandiseOrderId,
            SourceCode = order.ExternalId,
            EventType = EventType.MerchadiseStatus,
            Status = order.Status,
            Note = $"Created complaint handling order from {report.ExternalId}",
            CreatedDate = now
        }, cancellationToken);
        return OperationResult<MerchandiseOrder>.Ok(order);
    }

    private OperationResult<MerchandiseOrder> SynchronizeExisting(
        MerchandiseOrder order,
        IReadOnlyCollection<ReplacementLine> lines,
        Guid employeeId,
        DateTime now,
        DateTime? requestedDeliveryDate)
    {
        if (order.OrderType != OrderType.Complaint)
        {
            return OperationResult<MerchandiseOrder>.Fail("Đơn liên kết complaint không có OrderType Complaint.");
        }

        var expected = lines.ToDictionary(x => x.ComplaintReportLineId);
        var activeDetails = order.MerchandiseOrderDetails.Where(x => x.IsActive).ToList();
        if (activeDetails.Where(x => x.ComplaintReportLineId.HasValue)
            .GroupBy(x => x.ComplaintReportLineId!.Value).Any(x => x.Count() > 1))
        {
            return OperationResult<MerchandiseOrder>.Fail(
                "Đơn xử lý có nhiều detail active trỏ cùng một ComplaintReportLineId.");
        }

        if (string.Equals(order.Status, MerchadiseStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var valid = activeDetails.Count == expected.Count && activeDetails.All(x =>
                x.ComplaintReportLineId.HasValue && expected.TryGetValue(x.ComplaintReportLineId.Value, out var line) &&
                x.ProductId == line.ProductId && x.FormulaId == line.FormulaId &&
                x.ExpectedQuantity == line.ApprovedQuantity && x.UnitPriceAgreed == 0 && x.TotalPriceAgreed == 0);
            return valid
                ? OperationResult<MerchandiseOrder>.Ok(order, "Đơn xử lý đã đồng bộ và được duyệt trước đó.")
                : OperationResult<MerchandiseOrder>.Fail("Đơn xử lý Approved không còn khớp quyết định sản xuất bù.");
        }

        if (!string.Equals(order.Status, MerchadiseStatus.New.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<MerchandiseOrder>.Fail("Chỉ có thể đồng bộ đơn xử lý New hoặc xác nhận đơn Approved idempotent.");
        }

        foreach (var detail in activeDetails.Where(x =>
            !x.ComplaintReportLineId.HasValue || !expected.ContainsKey(x.ComplaintReportLineId.Value)))
        {
            detail.IsActive = false;
        }

        foreach (var line in lines)
        {
            var detail = activeDetails.SingleOrDefault(x =>
                x.ComplaintReportLineId == line.ComplaintReportLineId);
            if (detail is null)
            {
                order.MerchandiseOrderDetails.Add(CreateDetail(
                    order.MerchandiseOrderId, line, employeeId, now,
                    requestedDeliveryDate ?? now));
                continue;
            }

            detail.ProductId = line.ProductId;
            detail.ProductExternalIdSnapshot = line.ProductExternalId;
            detail.ProductNameSnapshot = line.ProductName;
            detail.FormulaId = line.FormulaId;
            detail.FormulaExternalIdSnapshot = line.FormulaExternalId;
            detail.ExpectedQuantity = line.ApprovedQuantity!.Value;
            detail.RecommendedUnitPrice = 0;
            detail.UnitPriceAgreed = 0;
            detail.TotalPriceAgreed = 0;
        }

        order.TotalPrice = 0;
        order.UpdatedDate = now;
        order.UpdatedBy = employeeId;
        return OperationResult<MerchandiseOrder>.Ok(order, "Đã đồng bộ đơn xử lý complaint.");
    }

    private static MerchandiseOrderDetail CreateDetail(
        Guid orderId,
        ReplacementLine line,
        Guid employeeId,
        DateTime now,
        DateTime deliveryDate)
        => new()
        {
            MerchandiseOrderDetailId = Guid.CreateVersion7(),
            MerchandiseOrderId = orderId,
            ComplaintReportLineId = line.ComplaintReportLineId,
            ProductId = line.ProductId,
            ProductExternalIdSnapshot = line.ProductExternalId,
            ProductNameSnapshot = line.ProductName,
            FormulaId = line.FormulaId,
            FormulaExternalIdSnapshot = line.FormulaExternalId,
            ExpectedQuantity = line.ApprovedQuantity!.Value,
            BagType = line.BagType,
            PackageWeight = line.PackageWeight,
            Status = MerchadiseStatus.New.ToString(),
            Comment = line.Description,
            DeliveryRequestDate = deliveryDate,
            BaseCostSnapshot = line.BaseCost,
            RecommendedUnitPrice = 0,
            UnitPriceAgreed = 0,
            TotalPriceAgreed = 0,
            IsActive = true
        };

    private Task<Manager?> FindManagerAsync(Guid employeeId, Guid companyId, CancellationToken cancellationToken)
        => _dbContext.Employees.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new Manager(x.EmployeeId, x.FullName, x.ExternalId))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record Manager(Guid EmployeeId, string FullName, string ExternalId);

    private sealed record ReplacementLine(
        Guid ComplaintReportLineId,
        Guid ProductId,
        string ProductExternalId,
        string ProductName,
        Guid FormulaId,
        string FormulaExternalId,
        decimal? ApprovedQuantity,
        decimal ComplaintQuantity,
        string? Description,
        string BagType,
        string PackageWeight,
        decimal BaseCost,
        MerchandiseOrder SourceOrder);
}
