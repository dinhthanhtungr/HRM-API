using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Orders;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services;

/// <summary>
/// Validate và dựng đầy đủ aggregate SaleOrder ở trạng thái New cùng các side effect trong DbContext.
/// Caller sở hữu transaction, SaveChanges và commit để có thể tái sử dụng trong create thường hoặc create kèm file.
/// </summary>
internal sealed class SaleOrderCreationService
{
    private const string CustomerReturnRequestTitle = "PHIẾU YÊU CẦU NHẬP HÀNG TRẢ VỀ TỪ KH";
    private readonly HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups.CustomerSampleSharingService _sampleSharing;
    private readonly ISaleOrderDbContext _dbContext;
    private readonly IExternalIdService _externalIdService;
    private readonly IEventLogWriter _eventLogWriter;
    private readonly SaleOrderLeadConversionService _leadConversionService;
    private readonly SaleOrderStandardSampleService _standardSamples;
    private readonly HRM.Application.Features.PLM.SaleOrders.DeliverySchedules.DeliveryScheduleService? _deliverySchedules;

    public SaleOrderCreationService(
        ISaleOrderDbContext dbContext,
        IExternalIdService externalIdService,
        IEventLogWriter eventLogWriter,
        SaleOrderLeadConversionService leadConversionService,
        SaleOrderStandardSampleService standardSamples,
        HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups.CustomerSampleSharingService sampleSharing,
        HRM.Application.Features.PLM.SaleOrders.DeliverySchedules.DeliveryScheduleService? deliverySchedules = null)
    {
        _dbContext = dbContext;
        _externalIdService = externalIdService;
        _eventLogWriter = eventLogWriter;
        _leadConversionService = leadConversionService;
        _standardSamples = standardSamples;
        _sampleSharing = sampleSharing;
        _deliverySchedules = deliverySchedules;
    }

    public async Task<OperationResult<MerchandiseOrder>> CreateAsync(
        CreateSaleOrderRequest request,
        Guid employeeId,
        Guid companyId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty || request.SaleOrderDetails is null)
        {
            return OperationResult<MerchandiseOrder>.Fail("CustomerId không hợp lệ.");
        }

        if (!Enum.IsDefined(request.OrderType))
        {
            return OperationResult<MerchandiseOrder>.Fail("OrderType không hợp lệ.");
        }

        var details = request.SaleOrderDetails
            .Where(x => x.ProductId != Guid.Empty && x.FormulaId != Guid.Empty && x.ExpectedQuantity > 0)
            .ToList();
        if (details.Count == 0)
        {
            return OperationResult<MerchandiseOrder>.Fail(
                "Đơn hàng phải có ít nhất một dòng hợp lệ.");
        }

        if (details.Any(x => x.UnitPriceAgreed < 0))
        {
            return OperationResult<MerchandiseOrder>.Fail("UnitPriceAgreed không được âm.");
        }

        if (request.OrderType == OrderType.Merchandise &&
            details.Any(x => x.UnitPriceAgreed <= 0))
        {
            return OperationResult<MerchandiseOrder>.Fail(
                "Đơn Hàng hóa phải có UnitPriceAgreed lớn hơn 0.");
        }

        var managerResult = await ResolveOrderManagerAsync(
            request.CustomerId,
            companyId,
            employeeId,
            cancellationToken);
        if (!managerResult.Success || managerResult.Data is null)
        {
            return OperationResult<MerchandiseOrder>.Fail(
                managerResult.Message ?? "Không thể xác định sale phụ trách khách hàng.");
        }

        var detailFormulaIds = details.Select(x => x.FormulaId).Distinct().ToArray();
        if (_sampleSharing.Enabled)
        {
            var owners = await _sampleSharing.ResolveOwnerIdsAsync(companyId, request.CustomerId, cancellationToken, forWrite: true);
            if (owners is null) return OperationResult<MerchandiseOrder>.Fail("Không có quyền lên đơn cho khách hàng này.");
            var customerCode = await _dbContext.Customers.AsNoTracking().Where(x => x.CompanyId == companyId && x.CustomerId == request.CustomerId)
                .Select(x => x.ExternalId).FirstOrDefaultAsync(cancellationToken);
            if (!HRM.Application.Features.PLM.Shared.Rules.PLMCustomerRules.CanUseAllCustomersFormula(request.OrderType,
                HRM.Application.Features.PLM.Shared.Rules.PLMCustomerRules.IsInternalCustomerExternalId(customerCode)))
            {
                var eligible = await _sampleSharing.EligibleProductsAsync(companyId, owners,
                    details.Select(x => x.ProductId).Distinct().ToArray(), cancellationToken);
                if (details.Any(x => !eligible.Contains(x.ProductId)))
                    return OperationResult<MerchandiseOrder>.Fail("Mẫu không thuộc khách đặt hàng hoặc nhóm dùng chung mẫu còn hoạt động.");
            }
        }
        var formulaRows = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                detailFormulaIds.Contains(x.FormulaId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.FormulaId,
                x.ProductId,
                x.ExternalId,
                x.Status
            })
            .ToListAsync(cancellationToken);
        var formulaById = formulaRows.ToDictionary(x => x.FormulaId);

        var unfinishedWorkflowFormulas = await LoadUnfinishedWorkflowFormulaIdsAsync(companyId, detailFormulaIds, cancellationToken);
        foreach (var detail in details)
        {
            if (!formulaById.TryGetValue(detail.FormulaId, out var formula))
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    $"FormulaId không tồn tại hoặc không thuộc công ty hiện tại: {detail.FormulaId}.");
            }

            if (formula.ProductId != detail.ProductId)
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    $"Công thức {formula.ExternalId} không thuộc sản phẩm của dòng đơn.");
            }

            if (formula.Status is not (nameof(FormulaStatus.SampleSent) or nameof(FormulaStatus.Completed)))
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    $"Công thức {formula.ExternalId} phải ở trạng thái SampleSent hoặc Completed để lên đơn hàng.");
            }
            if (unfinishedWorkflowFormulas.Contains(formula.FormulaId))
                return OperationResult<MerchandiseOrder>.Fail("Sale cần hoàn thành hồ sơ mẫu trước khi dùng công thức này để lên đơn.");
        }

        var review = await _standardSamples.ReviewAsync(request, companyId, now, cancellationToken);
        if (!review.Success || review.Data is null)
            return OperationResult<MerchandiseOrder>.Fail(review.Message ?? "Không thể kiểm tra mẫu chuẩn.");
        var requiredSamples = review.Data.Select(x => (x.ProductId, x.FormulaId)).ToHashSet();
        if (details.Any(x => requiredSamples.Contains((x.ProductId, x.FormulaId)) && !x.StandardSampleReviewed))
            return OperationResult<MerchandiseOrder>.Fail("Cần kiểm tra và xác nhận dialog mẫu chuẩn trước khi lưu đơn.");

        var orderId = request.MerchandiseOrderId == Guid.Empty
            ? Guid.CreateVersion7()
            : request.MerchandiseOrderId;
        var attachmentCollectionId = request.AttachmentCollectionId;
        if (attachmentCollectionId == Guid.Empty)
        {
            attachmentCollectionId = Guid.CreateVersion7();
            await _dbContext.AttachmentCollections.AddAsync(new AttachmentCollection
            {
                AttachmentCollectionId = attachmentCollectionId
            }, cancellationToken);
        }
        else
        {
            var collectionExists = await _dbContext.AttachmentCollections
                .AsNoTracking()
                .AnyAsync(
                    x => x.AttachmentCollectionId == attachmentCollectionId,
                    cancellationToken);
            if (!collectionExists)
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    "AttachmentCollectionId không tồn tại.");
            }
        }

        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = orderId,
            ExternalId = await _externalIdService.GenerateMonthlyCodeAsync(
                companyId,
                DocumentPrefix.DHG.ToString(),
                cancellationToken),
            OrderType = request.OrderType,
            AttachmentCollectionId = attachmentCollectionId,
            CustomerId = request.CustomerId,
            CustomerNameSnapshot = TrimToEmpty(request.CustomerNameSnapshot),
            CustomerExternalIdSnapshot = TrimToEmpty(request.CustomerExternalIdSnapshot),
            PhoneSnapshot = TrimToEmpty(request.PhoneSnapshot),
            ManagerById = managerResult.Data.EmployeeId,
            ManagerByNameSnapshot = managerResult.Data.FullName,
            ManagerExternalIdSnapshot = managerResult.Data.ExternalId,
            Receiver = TrimToEmpty(request.Receiver),
            DeliveryAddress = TrimToEmpty(request.DeliveryAddress),
            PaymentType = TrimToNull(request.PaymentType),
            Vat = request.Vat,
            Status = MerchadiseStatus.New.ToString(),
            Currency = TrimToNull(request.Currency),
            ExchangeRate = request.ExchangeRate,
            CompanyId = companyId,
            IsPaid = request.IsPaid,
            IsActive = true,
            PaymentDate = request.PaymentDate,
            Note = TrimToNull(request.Note),
            ShippingMethod = TrimToNull(request.ShippingMethod),
            PONo = TrimToEmpty(request.PONo),
            CreateDate = now,
            CreatedBy = employeeId,
            UpdatedDate = now,
            UpdatedBy = employeeId
        };

        List<MerchandiseOrderDetail> sourceLines = [];
        if (request.OrderType == OrderType.Complaint)
        {
            if (request.SourceMerchandiseOrderId is not { } sourceOrderId || sourceOrderId == Guid.Empty)
                return OperationResult<MerchandiseOrder>.Fail("Chọn ĐHG gốc cho đơn khiếu nại.");

            sourceLines = await _dbContext.MerchandiseOrderDetails.AsNoTracking()
                .Where(x => x.MerchandiseOrderId == sourceOrderId && x.IsActive
                    && x.MerchandiseOrder.IsActive && x.MerchandiseOrder.CompanyId == companyId
                    && x.MerchandiseOrder.CustomerId == request.CustomerId)
                .ToListAsync(cancellationToken);
            if (sourceLines.Count == 0)
                return OperationResult<MerchandiseOrder>.Fail("ĐHG gốc không hợp lệ.");

            var creatorName = await _dbContext.Employees.AsNoTracking()
                .Where(x => x.EmployeeId == employeeId && x.CompanyId == companyId)
                .Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken);
            var report = new ComplaintReport
            {
                ComplaintReportId = Guid.CreateVersion7(),
                ExternalId = await _externalIdService.GenerateMonthlyCodeAsync(companyId, DocumentPrefix.CPL.ToString(), cancellationToken),
                CompanyId = companyId,
                CustomerId = order.CustomerId,
                CustomerExternalIdSnapshot = order.CustomerExternalIdSnapshot,
                CustomerNameSnapshot = order.CustomerNameSnapshot,
                AttachmentCollection = new AttachmentCollection { AttachmentCollectionId = Guid.CreateVersion7() },
                Status = ComplaintReportStatus.Draft,
                Summary = order.Note,
                ReportedAt = now,
                CreatedDate = now,
                CreatedBy = employeeId,
                CreatedByNameSnapshot = TrimToEmpty(creatorName),
                UpdatedDate = now,
                UpdatedBy = employeeId
            };
            order.ComplaintReport = report;
            order.ComplaintReportId = report.ComplaintReportId;
        }
        else if (request.SourceMerchandiseOrderId.HasValue || details.Any(x =>
                     x.SourceMerchandiseOrderDetailId.HasValue || x.ReplacementSourceManufacturingFormulaId.HasValue))
        {
            return OperationResult<MerchandiseOrder>.Fail("Chỉ đơn khiếu nại được chọn ĐHG gốc và VA nguồn.");
        }

        var returnLines = new List<WarehouseRequestDetail>();
        foreach (var detailRequest in details)
        {
            var totalPrice = Math.Round(
                detailRequest.UnitPriceAgreed * detailRequest.ExpectedQuantity,
                2,
                MidpointRounding.AwayFromZero);
            var newDetail = new MerchandiseOrderDetail
            {
                MerchandiseOrderDetailId = detailRequest.MerchandiseOrderDetailId == Guid.Empty
                    ? Guid.CreateVersion7()
                    : detailRequest.MerchandiseOrderDetailId,
                MerchandiseOrderId = orderId,
                ProductId = detailRequest.ProductId,
                ProductExternalIdSnapshot = TrimToEmpty(detailRequest.ProductExternalIdSnapshot),
                ProductNameSnapshot = TrimToEmpty(detailRequest.ProductNameSnapshot),
                FormulaId = detailRequest.FormulaId,
                FormulaExternalIdSnapshot = TrimToEmpty(detailRequest.FormulaExternalIdSnapshot),
                ExpectedQuantity = detailRequest.ExpectedQuantity,
                RealQuantity = detailRequest.RealQuantity,
                BagType = TrimToEmpty(detailRequest.BagType),
                PackageWeight = TrimToEmpty(detailRequest.PackageWeight),
                Status = requiredSamples.Contains((detailRequest.ProductId, detailRequest.FormulaId)) && !detailRequest.HasStandardSample
                    ? nameof(MerchadiseStatus.WaitingForStandardSample) : order.Status,
                Comment = TrimToNull(detailRequest.Comment),
                DeliveryRequestDate = detailRequest.DeliveryRequestDate,
                DeliveryActualDate = detailRequest.DeliveryActualDate,
                ExpectedDeliveryDate = detailRequest.ExpectedDeliveryDate,
                BaseCostSnapshot = detailRequest.BaseCostSnapshot,
                RecommendedUnitPrice = detailRequest.RecommendedUnitPrice,
                UnitPriceAgreed = detailRequest.UnitPriceAgreed,
                TotalPriceAgreed = totalPrice,
                IsActive = true
            };
            if (order.ComplaintReport is { } complaint)
            {
                var matches = sourceLines.Where(x => x.ProductId == detailRequest.ProductId
                    && (!detailRequest.SourceMerchandiseOrderDetailId.HasValue
                        || x.MerchandiseOrderDetailId == detailRequest.SourceMerchandiseOrderDetailId))
                    .Take(2).ToList();
                if (matches.Count != 1)
                    return OperationResult<MerchandiseOrder>.Fail("Chọn đúng dòng sản phẩm thuộc ĐHG gốc.");

                var source = matches[0];
                if (detailRequest.ReplacementSourceManufacturingFormulaId is { } vaId)
                {
                    var options = await ComplaintOrderSourceReader.ReadVaAsync(
                        _dbContext, companyId, source.MerchandiseOrderDetailId, cancellationToken);
                    var selectedVa = options.SingleOrDefault(x => x.ManufacturingFormulaId == vaId);
                    if (selectedVa is null)
                        return OperationResult<MerchandiseOrder>.Fail("VA nguồn không thuộc dòng hàng của ĐHG gốc.");
                    newDetail.ReplacementSourceManufacturingFormulaId = vaId;
                    returnLines.Add(new WarehouseRequestDetail
                    {
                        ProductCode = source.ProductExternalIdSnapshot,
                        ProductName = source.ProductNameSnapshot,
                        LotNumber = selectedVa.ExternalId,
                        WeightKg = newDetail.ExpectedQuantity,
                        UnitName = "Kg",
                        ItemStockType = StockType.Waiter,
                        StockStatus = nameof(StockType.Waiter)
                    });
                }

                var complaintLine = new ComplaintReportLine
                {
                    ComplaintReportLineId = Guid.CreateVersion7(),
                    ComplaintReportId = complaint.ComplaintReportId,
                    SourceMerchandiseOrderDetailId = source.MerchandiseOrderDetailId,
                    ProductId = source.ProductId,
                    ProductExternalIdSnapshot = source.ProductExternalIdSnapshot,
                    ProductNameSnapshot = source.ProductNameSnapshot,
                    FormulaId = source.FormulaId,
                    FormulaExternalIdSnapshot = source.FormulaExternalIdSnapshot,
                    ComplaintQuantity = newDetail.ExpectedQuantity,
                    CreatedDate = now,
                    CreatedBy = employeeId,
                    UpdatedDate = now,
                    UpdatedBy = employeeId
                };
                complaint.ComplaintReportLines.Add(complaintLine);
                newDetail.ComplaintReportLineId = complaintLine.ComplaintReportLineId;
            }
            if (detailRequest.DeliverySchedules is not null)
            {
                var scheduleError = _deliverySchedules?.PrepareNewDetail(newDetail, detailRequest.DeliverySchedules, employeeId, now)
                    ?? (_deliverySchedules is null ? "Luồng chia đợt chưa sẵn sàng." : null);
                if (scheduleError is not null) return OperationResult<MerchandiseOrder>.Fail(scheduleError);
            }
            order.MerchandiseOrderDetails.Add(newDetail);
        }

        order.TotalPrice = Math.Round(
            order.MerchandiseOrderDetails.Sum(x => x.TotalPriceAgreed),
            2,
            MidpointRounding.AwayFromZero);

        foreach (var detail in order.MerchandiseOrderDetails.Where(x => requiredSamples.Contains((x.ProductId, x.FormulaId))))
        {
            await _eventLogWriter.AddAsync(new EventLogCreateRequest
            {
                EmployeeId = employeeId, CompanyId = companyId, SourceType = nameof(MerchandiseOrderDetail),
                SourceId = detail.MerchandiseOrderDetailId, ParentSourceType = nameof(MerchandiseOrder),
                ParentSourceId = orderId, SourceCode = order.ExternalId, EventType = EventType.MerchadiseStatus,
                Status = detail.Status, CreatedDate = now,
                Note = detail.Status == nameof(MerchadiseStatus.WaitingForStandardSample)
                    ? "Đang chờ mẫu chuẩn." : "Sale xác nhận đã có mẫu chuẩn.",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { detail.ProductId, detail.FormulaId })
            }, cancellationToken);
        }

        await _leadConversionService.ApplyAsync(order, employeeId, now, cancellationToken);
        if (returnLines.Count > 0)
        {
            _dbContext.WarehouseRequests.Add(new WarehouseRequest
            {
                RequestCode = await _externalIdService.GenerateMonthlyCodeAsync(
                    companyId, DocumentPrefix.PRQ.ToString(), cancellationToken),
                RequestName = CustomerReturnRequestTitle,
                ReqType = WareHouseRequestType.ImportReturn,
                ReqStatus = WarehouseRequestStatus.Pending,
                codeFromRequest = order.ExternalId,
                CompanyId = companyId,
                CreatedDate = now,
                CreatedBy = employeeId,
                UpdatedDate = now,
                UpdatedBy = employeeId,
                WarehouseRequestDetails = returnLines
            });
        }

        await _dbContext.MerchandiseOrders.AddAsync(order, cancellationToken);
        await _eventLogWriter.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId,
            CompanyId = order.CompanyId,
            SourceType = "MerchandiseOrder",
            SourceId = order.MerchandiseOrderId,
            SourceCode = order.ExternalId,
            EventType = EventType.MerchadiseStatus,
            Status = order.Status,
            Note = $"Created Merchandise Order {order.ExternalId}",
            CreatedDate = now
        }, cancellationToken);

        return OperationResult<MerchandiseOrder>.Ok(order);
    }

    public static CreateSaleOrderResultDto ToResultDto(MerchandiseOrder order)
    {
        return new CreateSaleOrderResultDto
        {
            MerchandiseOrderId = order.MerchandiseOrderId,
            ExternalId = order.ExternalId,
            AttachmentCollectionId = order.AttachmentCollectionId,
            Status = order.Status
        };
    }

    internal async Task<HashSet<Guid>> LoadUnfinishedWorkflowFormulaIdsAsync(Guid companyId, Guid[] detailFormulaIds,
        CancellationToken cancellationToken)
    {
        var unfinished = (await _dbContext.SampleRequestSampleTrials.AsNoTracking()
            .Where(x => x.IsActive && x.FormulaId.HasValue && detailFormulaIds.Contains(x.FormulaId.Value) &&
                x.SentDate != null && x.SampleRequest.IsActive && x.SampleRequest.CompanyId == companyId &&
                x.SampleRequest.CreatedDate >= SampleRequests.Services.Workflow.SampleFlowRules.StartsAt &&
                x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled")
            .Select(x => x.FormulaId!.Value).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        // Quotation finalization has no Trial: its persisted event links the VU until Sale completes the request.
        var quotationIds = await (from evt in _dbContext.SampleRequestWorkflowEvents.AsNoTracking()
                                  join request in _dbContext.SampleRequests.AsNoTracking() on evt.SampleRequestId equals request.SampleRequestId
                                  where evt.CompanyId == companyId && request.CompanyId == companyId && request.IsActive &&
                                      request.CreatedDate >= SampleRequests.Services.Workflow.SampleFlowRules.StartsAt &&
                                      request.Status != "Completed" && request.Status != "Cancelled" &&
                                      evt.EventType == SampleRequests.Services.Workflow.SampleFlowRules.QuotationFinalized &&
                                      evt.FormulaId.HasValue && detailFormulaIds.Contains(evt.FormulaId.Value)
                                  select evt.FormulaId!.Value).Distinct().ToListAsync(cancellationToken);
        unfinished.UnionWith(quotationIds);
        return unfinished;
    }

    private static string TrimToEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Resolve the sale responsible for the customer from CRM data, never from client fields.
    /// </summary>
    private async Task<OperationResult<OrderManagerSnapshot>> ResolveOrderManagerAsync(
        Guid customerId,
        Guid companyId,
        Guid creatingEmployeeId,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.IsActive != false)
            .Select(x => new
            {
                x.IsLead,
                x.CurrentSaleId,
                LatestAssignmentEmployeeId = x.CustomerAssignments
                    .Where(assignment => assignment.IsActive && assignment.CompanyId == companyId)
                    .OrderByDescending(assignment => assignment.CreatedDate)
                    .Select(assignment => (Guid?)assignment.EmployeeId)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return OperationResult<OrderManagerSnapshot>.Fail(
                "Không tìm thấy khách hàng trong công ty hiện tại.");
        }

        var resolvedManagerId = customer.IsLead
            ? creatingEmployeeId
            : customer.LatestAssignmentEmployeeId ?? customer.CurrentSaleId ?? creatingEmployeeId;

        var manager = await FindActiveEmployeeAsync(
            resolvedManagerId,
            companyId,
            cancellationToken);
        if (manager is not null)
        {
            return OperationResult<OrderManagerSnapshot>.Ok(manager);
        }

        if (resolvedManagerId != creatingEmployeeId)
        {
            manager = await FindActiveEmployeeAsync(
                creatingEmployeeId,
                companyId,
                cancellationToken);
            if (manager is not null)
            {
                return OperationResult<OrderManagerSnapshot>.Ok(manager);
            }
        }

        return OperationResult<OrderManagerSnapshot>.Fail(
            "Không tìm thấy sale phụ trách hoạt động trong công ty hiện tại.");
    }

    private Task<OrderManagerSnapshot?> FindActiveEmployeeAsync(
        Guid employeeId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _dbContext.Employees
            .AsNoTracking()
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new OrderManagerSnapshot(x.EmployeeId, x.FullName, x.ExternalId))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record OrderManagerSnapshot(Guid EmployeeId, string FullName, string ExternalId);
}
