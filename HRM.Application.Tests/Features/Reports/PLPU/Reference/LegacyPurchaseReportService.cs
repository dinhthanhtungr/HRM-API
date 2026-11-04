using HRM.Application.Features.Reports.PLPU;

namespace HRM.Application.Tests.Features.Reports.PLPU.Reference
{
    public class PLPUPurchaseOverviewReportService
    {
        private const string StatusCancelled = "Cancelled";
        private const string StatusCanceled = "Canceled";
        private const string StatusClosed = "Closed";

        private static readonly Guid MaterialPackagingCategoryId =
            Guid.Parse("3bed94ed-da05-4e5f-ac04-c7647aaa63d6");

        private const decimal CompletionTolerancePercent = 2m;
        private const decimal OverDeliveryTolerancePercent = 2m;

        private readonly IPLPUPurchaseOverviewReportRepository _repository;

        public PLPUPurchaseOverviewReportService(IPLPUPurchaseOverviewReportRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Lấy toàn bộ dữ liệu tổng quan mua hàng: summary, danh sách PO cần xử lý, biểu đồ NCC.
        /// </summary>
        public async Task<PLPUPurchaseOverviewReportDto> GetOverviewAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct = default)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts);
            var details = BuildDetailRows(rawData.OrderLines, allocatedReceipts);
            var headers = BuildHeaderRows(rawData.OrderLines, details);

            if (query.OnlyNeedAction)
                headers = headers.Where(x => x.NeedBuyerAction).ToList();

            var pageNumber = NormalizePageNumber(query.PageNumber);
            var pageSize = NormalizePageSize(query.PageSize);

            var pagedHeaders = headers
                .OrderByDescending(x => x.NeedBuyerAction)
                .ThenByDescending(x => x.IsOverdue)
                .ThenByDescending(x => x.HasQCFail)
                .ThenByDescending(x => x.HasPendingQC)
                .ThenBy(x => x.RequestDeliveryDate ?? DateTime.MaxValue)
                .ThenByDescending(x => x.CreatedDate)
                .ToList();

            var summary = BuildSummary(headers, details);

            var localizedPagedHeaders = LocalizeHeaders(pagedHeaders);

            var localizedNeedActionHeaders = LocalizeHeaders(
                headers
                    .Where(x => x.NeedBuyerAction)
                    .OrderByDescending(x => x.IsOverdue)
                    .ThenByDescending(x => x.HasQCFail)
                    .ThenByDescending(x => x.HasPendingQC)
                    .ThenByDescending(x => x.HasOverDelivery)
                    .ThenBy(x => x.RequestDeliveryDate ?? DateTime.MaxValue)
                    .Take(20)
            );

            return new PLPUPurchaseOverviewReportDto
            {
                Summary = summary,
                PurchaseOrders = ToPagedResult(localizedPagedHeaders, pageNumber, pageSize),
                NeedActionPurchaseOrders = localizedNeedActionHeaders,
                SupplierPerformance = BuildSupplierPerformance(rawData.OrderLines, details, headers)
                    .OrderByDescending(x => x.RiskScore)
                    .ThenBy(x => x.SupplierName)
                    .ToList()
            };
        }

        /// <summary>
        /// Lấy dữ liệu header dashboard cho báo cáo mua hàng.
        /// </summary>
        public async Task<PLPUPurchaseOrderHeaderDashboardDto> GetHeaderReportAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct = default)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts);
            var details = BuildDetailRows(rawData.OrderLines, allocatedReceipts);
            var headers = BuildHeaderRows(rawData.OrderLines, details);

            return BuildHeaderDashboard(rawData.OrderLines, details, headers);
        }

        /// <summary>
        /// Lấy danh sách PO dạng phân trang cho màn hình chính.
        /// </summary>
        public async Task<PLPUPagedResult<PLPUPurchaseOrderRowDto>> GetRowsAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct = default)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts);
            var details = BuildDetailRows(rawData.OrderLines, allocatedReceipts);
            var headers = BuildHeaderRows(rawData.OrderLines, details);

            if (query.OnlyNeedAction)
                headers = headers.Where(x => x.NeedBuyerAction).ToList();

            var rows = headers
                .OrderByDescending(x => x.NeedBuyerAction)
                .ThenByDescending(x => x.IsOverdue)
                .ThenByDescending(x => x.HasQCFail)
                .ThenBy(x => x.RequestDeliveryDate ?? DateTime.MaxValue)
                .ThenByDescending(x => x.CreatedDate)
                .Select(x => new PLPUPurchaseOrderRowDto
                {
                    PurchaseOrderId = x.PurchaseOrderId,
                    POExternalId = x.POExternalId,
                    OrderType = x.OrderType,

                    SupplierId = x.SupplierId,
                    SupplierName = x.SupplierName,

                    CreatedDate = x.CreatedDate,
                    RequestDeliveryDate = x.RequestDeliveryDate,
                    RealDeliveryDate = x.RealDeliveryDate,
                    LastReceiptDate = x.LastReceiptDate,

                    POStatus = x.POStatus,
                    ReportStatus = ToVietnameseStatus(x.ReportStatus),
                    AlertFlags = ToVietnameseFlags(x.AlertFlags),

                    TotalLines = x.TotalLines,

                    OrderedQuantity = x.OrderedQuantity,
                    WarehouseReceivedQuantity = x.WarehouseReceivedQuantity,
                    AcceptedQuantity = x.AcceptedQuantity,
                    PendingQcQuantity = x.PendingQcQuantity,
                    RejectedQuantity = x.RejectedQuantity,

                    RemainingToAccept = x.RemainingToAccept,
                    CompletionPercent = x.CompletionPercent,
                    TotalPurchaseValue = x.TotalPurchaseValue,
                    ReceivedPurchaseValue = x.ReceivedPurchaseValue,

                    IsOverdue = x.IsOverdue,
                    DelayDays = x.DelayDays,

                    NeedBuyerAction = x.NeedBuyerAction,
                    BuyerAction = ToVietnameseBuyerAction(x.BuyerAction),

                    PLPUComment = x.PLPUComment
                })
                .ToList();

            return ToPagedResult(rows, NormalizePageNumber(query.PageNumber), NormalizePageSize(query.PageSize));
        }

        /// <summary>
        /// Lấy chi tiết một PO gồm header, lines, phiếu nhập và QC.
        /// </summary>
        public async Task<PLPUPurchaseOrderDetailDashboardDto> GetOrderDetailAsync(
            Guid purchaseOrderId,
            CancellationToken ct = default)
        {
            var query = new PLPUPurchaseOverviewQuery
            {
                PurchaseOrderId = purchaseOrderId,
                IncludeInactive = true
            };

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts);
            var details = BuildDetailRows(rawData.OrderLines, allocatedReceipts);
            var headers = BuildHeaderRows(rawData.OrderLines, details);
            var qcRows = BuildQcRowsFromAllocatedReceipts(allocatedReceipts);

            PLPUPurchaseOrderDetailDashboardDto temp = new PLPUPurchaseOrderDetailDashboardDto
            {
                Header = LocalizeHeaders(headers).FirstOrDefault() ?? new PLPUPurchaseOrderHeaderReportDto(),

                Lines = LocalizeDetails(
                    details
                        .OrderBy(x => x.LineNo)
                        .ThenBy(x => x.MaterialCode)
                ),

                Receipts = LocalizeReceipts(
                    allocatedReceipts
                        .OrderByDescending(x => x.ReceiptDate)
                        .ThenBy(x => x.LineNo)
                        .ThenBy(x => x.MaterialCode())
                ),

                QcRows = LocalizeQcRows(
                    qcRows
                        .OrderByDescending(x => x.QCGroup == "Rejected")
                        .ThenByDescending(x => x.QCGroup == "Pending")
                        .ThenByDescending(x => x.ReceiptDate)
                )
            };

            return temp;
        }

        /// <summary>
        /// Lấy danh sách dòng chi tiết PO dạng phân trang.
        /// </summary>
        public async Task<PLPUPagedResult<PLPUPurchaseOrderDetailReportDto>> GetDetailLinesAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct = default)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts);

            var details = BuildDetailRows(rawData.OrderLines, allocatedReceipts)
                .OrderByDescending(x => x.NeedBuyerAction)
                .ThenBy(x => x.RequestDeliveryDate ?? DateTime.MaxValue)
                .ThenBy(x => x.POExternalId)
                .ThenBy(x => x.LineNo)
                .ToList();

            if (query.OnlyNeedAction)
                details = details.Where(x => x.NeedBuyerAction).ToList();

            return ToPagedResult(
                LocalizeDetails(details),
                NormalizePageNumber(query.PageNumber),
                NormalizePageSize(query.PageSize));
        }

        /// <summary>
        /// Lấy danh sách phiếu nhập kho liên quan PO.
        /// </summary>
        public async Task<PLPUPagedResult<PLPUPurchaseReceiptReportDto>> GetReceiptsAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct = default)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts)
                .OrderByDescending(x => x.ReceiptDate)
                .ThenByDescending(x => x.WarehouseVoucherDetailId)
                .ToList();

            return ToPagedResult(
                 LocalizeReceipts(allocatedReceipts),
                 NormalizePageNumber(query.PageNumber),
                 NormalizePageSize(query.PageSize));
        }

        /// <summary>
        /// Lấy danh sách kết quả QC từ các phiếu nhập kho.
        /// </summary>
        public async Task<PLPUPagedResult<PLPUPurchaseQcReportDto>> GetQcAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct = default)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts);
            var qcRows = BuildQcRowsFromAllocatedReceipts(allocatedReceipts)
                .OrderByDescending(x => x.QCGroup == "Rejected")
                .ThenByDescending(x => x.QCGroup == "Pending")
                .ThenByDescending(x => x.ReceiptDate)
                .ToList();

            return ToPagedResult(
                LocalizeQcRows(qcRows),
                NormalizePageNumber(query.PageNumber),
                NormalizePageSize(query.PageSize));
        }

        /// <summary>
        /// Lấy thống kê hiệu suất nhà cung cấp.
        /// </summary>
        public async Task<List<PLPUSupplierPurchasePerformanceDto>> GetSupplierPerformanceAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct = default)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            var rawData = await _repository.GetRawDataAsync(query, ct);

            var allocatedReceipts = AllocateReceiptsToOrderLines(rawData.OrderLines, rawData.Receipts);
            var details = BuildDetailRows(rawData.OrderLines, allocatedReceipts);
            var headers = BuildHeaderRows(rawData.OrderLines, details);

            return BuildSupplierPerformance(rawData.OrderLines, details, headers)
                .OrderByDescending(x => x.RiskScore)
                .ThenBy(x => x.SupplierName)
                .ToList();
        }


        // ========================================== Các hàm private chính ===========================================

        /// <summary>
        /// Phân bổ số lượng nhập kho vào từng dòng PO theo mã PO và mã vật tư.
        /// </summary>
        private static List<PLPUPurchaseReceiptReportDto> AllocateReceiptsToOrderLines(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseReceiptReportDto> receipts)
        {
            var result = new List<PLPUPurchaseReceiptReportDto>();

            if (orderLines.Count == 0 || receipts.Count == 0)
                return result;

            var linesByPoAndMaterial = orderLines
                .GroupBy(x => ReceiptMatchKey(x.POExternalId, x.MaterialCode))
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(r => r.LineNo)
                        .ThenBy(r => r.PurchaseOrderDetailId)
                        .ToList()
                );

            var allocatedPhysicalQtyByLine = orderLines.ToDictionary(
                x => x.PurchaseOrderDetailId,
                _ => 0m
            );

            foreach (var receiptGroup in receipts
                         .OrderBy(x => x.ReceiptDate)
                         .ThenBy(x => x.WarehouseVoucherDetailId)
                         .GroupBy(x => ReceiptMatchKey(x.POExternalId, x.ProductCode)))
            {
                if (!linesByPoAndMaterial.TryGetValue(receiptGroup.Key, out var candidateLines) ||
                    candidateLines.Count == 0)
                {
                    // Không tìm được line tương ứng thì vẫn giữ receipt để debug,
                    // nhưng PurchaseOrderDetailId = null.
                    result.AddRange(receiptGroup);
                    continue;
                }

                foreach (var receipt in receiptGroup)
                {
                    var remainingReceiptQty = receipt.QtyKg;

                    if (remainingReceiptQty <= 0)
                        continue;

                    while (remainingReceiptQty > 0)
                    {
                        var targetLine = candidateLines.FirstOrDefault(line =>
                        {
                            var allocated = allocatedPhysicalQtyByLine[line.PurchaseOrderDetailId];
                            return allocated < line.OrderedQuantity;
                        });

                        targetLine ??= candidateLines.Last();

                        var targetAllocated = allocatedPhysicalQtyByLine[targetLine.PurchaseOrderDetailId];
                        var targetRemaining = Positive(targetLine.OrderedQuantity - targetAllocated);

                        var allocatedQty = targetRemaining > 0
                            ? Math.Min(remainingReceiptQty, targetRemaining)
                            : remainingReceiptQty;

                        if (allocatedQty <= 0)
                            allocatedQty = remainingReceiptQty;

                        result.Add(CloneReceiptForLine(receipt, targetLine, allocatedQty));

                        allocatedPhysicalQtyByLine[targetLine.PurchaseOrderDetailId] += allocatedQty;
                        remainingReceiptQty -= allocatedQty;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Clone một dòng nhập kho và gán vào đúng dòng PO với số lượng đã phân bổ.
        /// </summary>
        private static PLPUPurchaseReceiptReportDto CloneReceiptForLine(
            PLPUPurchaseReceiptReportDto source,
            PLPUPurchaseOrderLineRaw line,
            decimal allocatedQty)
        {
            var ratio = source.QtyKg == 0 ? 0 : allocatedQty / source.QtyKg;
            var autoAccept = IsAutoAcceptPackagingQc(line);
            return new PLPUPurchaseReceiptReportDto
            {
                WarehouseRequestId = source.WarehouseRequestId,
                CodeFromRequest = source.CodeFromRequest,

                WarehouseVoucherId = source.WarehouseVoucherId,
                WarehouseVoucherCode = source.WarehouseVoucherCode,
                WarehouseVoucherDetailId = source.WarehouseVoucherDetailId,
                VoucherType = source.VoucherType,

                ReceiptDate = source.ReceiptDate,

                PurchaseOrderId = line.PurchaseOrderId,
                PurchaseOrderDetailId = line.PurchaseOrderDetailId,

                POExternalId = line.POExternalId,
                LineNo = line.LineNo,

                SupplierId = line.SupplierId,
                SupplierName = line.SupplierName,

                ProductCode = source.ProductCode,
                ProductName = string.IsNullOrWhiteSpace(source.ProductName) ? line.MaterialName : source.ProductName,
                LotNumber = source.LotNumber,

                QtyKg = allocatedQty,

                QCResult = source.QCResult,
                QCCreatedDate = source.QCCreatedDate,
                QCGroup = autoAccept ? "Accepted" : source.QCGroup,

                VoucherDetailType = source.VoucherDetailType,

                AcceptedQty = autoAccept
                    ? allocatedQty
                    : Math.Round(source.AcceptedQty * ratio, 6),

                                PendingQcQty = autoAccept
                    ? 0m
                    : Math.Round(source.PendingQcQty * ratio, 6),

                                RejectedQty = autoAccept
                    ? 0m
                    : Math.Round(source.RejectedQty * ratio, 6),
            };
        }

        /// <summary>
        /// Tạo dữ liệu báo cáo chi tiết từng dòng PO.
        /// </summary>
        private static List<PLPUPurchaseOrderDetailReportDto> BuildDetailRows(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseReceiptReportDto> allocatedReceipts)
        {
            var receiptsByDetailId = allocatedReceipts
                .Where(x => x.PurchaseOrderDetailId.HasValue)
                .GroupBy(x => x.PurchaseOrderDetailId!.Value)
                .ToDictionary(x => x.Key, x => x.OrderBy(r => r.ReceiptDate).ToList());

            return orderLines
                .Select(line =>
                {
                    receiptsByDetailId.TryGetValue(line.PurchaseOrderDetailId, out var receipts);
                    receipts ??= new List<PLPUPurchaseReceiptReportDto>();

                    var received = receipts.Sum(x => x.QtyKg);
                    var accepted = receipts.Sum(x => x.AcceptedQty);
                    var pendingQc = receipts.Sum(x => x.PendingQcQty);
                    var rejected = receipts.Sum(x => x.RejectedQty);

                    if (IsAutoAcceptPackagingQc(line))
                    {
                        accepted = received;
                        pendingQc = 0m;
                        rejected = 0m;
                    }

                    var dueDate = line.DeliveryDate ?? line.HeaderRequestDeliveryDate;

                    var firstReceipt = receipts.Count > 0 ? receipts.Min(x => x.ReceiptDate) : (DateTime?)null;
                    var lastReceipt = receipts.Count > 0 ? receipts.Max(x => x.ReceiptDate) : (DateTime?)null;

                    var dateWhenReceivedEnough = GetDateWhenEnough(
                        receipts.OrderBy(x => x.ReceiptDate),
                        line.OrderedQuantity,
                        x => x.QtyKg);

                    var dateWhenAcceptedEnough = GetDateWhenEnough(
                        receipts.OrderBy(x => x.ReceiptDate),
                        line.OrderedQuantity,
                        x => IsAutoAcceptPackagingQc(line) ? x.QtyKg : x.AcceptedQty);

                    var deliveryDelayDays = CalculateDelayDays(
                        dueDate,
                        dateWhenReceivedEnough,
                        received,
                        line.OrderedQuantity);

                    var acceptanceDelayDays = CalculateDelayDays(
                        dueDate,
                        dateWhenAcceptedEnough,
                        accepted,
                        line.OrderedQuantity);

                    var priceVariance = line.UnitPriceAgreed.HasValue && line.BaseCostSnapshot.HasValue
                        ? line.UnitPriceAgreed.Value - line.BaseCostSnapshot.Value
                        : (decimal?)null;

                    var effectiveUnitPrice = ResolveUnitPrice(line.OrderedQuantity, line.UnitPriceAgreed, line.TotalPriceAgreed, line.BaseCostSnapshot);
                    var receivedPurchaseValue = Math.Round(received * effectiveUnitPrice, 2, MidpointRounding.AwayFromZero);

                    var priceVariancePercent = priceVariance.HasValue &&
                                               line.BaseCostSnapshot.HasValue &&
                                               line.BaseCostSnapshot.Value != 0
                        ? priceVariance.Value / line.BaseCostSnapshot.Value * 100
                        : (decimal?)null;

                    var flags = ResolveLineAlertFlags(
                        ordered: line.OrderedQuantity,
                        received: received,
                        accepted: accepted,
                        pendingQc: pendingQc,
                        rejected: rejected,
                        dueDate: dueDate,
                        dateWhenReceivedEnough: dateWhenReceivedEnough);

                    var status = ResolveLinePrimaryStatus(
                        isActive: line.POIsActive,
                        poStatus: line.POStatus,
                        ordered: line.OrderedQuantity,
                        received: received,
                        accepted: accepted,
                        pendingQc: pendingQc,
                        rejected: rejected,
                        dueDate: dueDate);

                    var needAction = IsNeedActionLine(flags);



                    return new PLPUPurchaseOrderDetailReportDto
                    {
                        PurchaseOrderDetailId = line.PurchaseOrderDetailId,
                        PurchaseOrderId = line.PurchaseOrderId,
                        POExternalId = line.POExternalId,

                        LineNo = line.LineNo,

                        MaterialId = line.MaterialId,
                        MaterialCode = line.MaterialCode,
                        MaterialName = line.MaterialName,
                        Package = line.Package,

                        OrderedQuantity = line.OrderedQuantity,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = pendingQc,
                        RejectedQuantity = rejected,

                        RemainingToReceive = RemainingWithTolerance(line.OrderedQuantity, received),
                        RemainingToAccept = RemainingWithTolerance(line.OrderedQuantity, accepted),
                        OverReceivedQuantity = OverReceivedWithTolerance(received, line.OrderedQuantity),

                        DeliveryDate = line.DeliveryDate,
                        RequestDeliveryDate = dueDate,

                        FirstReceiptDate = firstReceipt,
                        LastReceiptDate = lastReceipt,
                        DateWhenReceivedEnough = dateWhenReceivedEnough,
                        DateWhenAcceptedEnough = dateWhenAcceptedEnough,

                        DeliveryDelayDays = deliveryDelayDays,
                        AcceptanceDelayDays = acceptanceDelayDays,
                        DelayDays = deliveryDelayDays,

                        LineReportStatus = status,
                        AlertFlags = string.Join(",", flags),

                        NeedBuyerAction = needAction,
                        BuyerAction = ResolveBuyerAction(flags),

                        BaseCostSnapshot = line.BaseCostSnapshot,
                        BaseDateSnapshot = line.BaseDateSnapshot,
                        UnitPriceAgreed = line.UnitPriceAgreed,
                        TotalPriceAgreed = line.TotalPriceAgreed,
                        ReceivedPurchaseValue = receivedPurchaseValue,

                        PriceVariance = priceVariance,
                        PriceVariancePercent = priceVariancePercent,
                        PriceImpactAmount = priceVariance.HasValue
                            ? priceVariance.Value * line.OrderedQuantity
                            : null,

                        Note = line.Note
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Gom các dòng chi tiết thành dữ liệu header theo từng PO.
        /// </summary>
        private static List<PLPUPurchaseOrderHeaderReportDto> BuildHeaderRows(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            var detailsByPo = details
                .GroupBy(x => x.PurchaseOrderId)
                .ToDictionary(x => x.Key, x => x.ToList());

            return orderLines
                .GroupBy(x => x.PurchaseOrderId)
                .Select(g =>
                {
                    var first = g.First();

                    detailsByPo.TryGetValue(first.PurchaseOrderId, out var poDetails);
                    poDetails ??= new List<PLPUPurchaseOrderDetailReportDto>();

                    var ordered = poDetails.Sum(x => x.OrderedQuantity);
                    var received = poDetails.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = poDetails.Sum(x => x.AcceptedQuantity);
                    var pendingQc = poDetails.Sum(x => x.PendingQcQuantity);
                    var rejected = poDetails.Sum(x => x.RejectedQuantity);

                    var totalValue = g.Sum(x =>
                        x.TotalPriceAgreed ??
                        x.OrderedQuantity * (x.UnitPriceAgreed ?? 0));

                    var receivedValue = poDetails.Sum(x => x.ReceivedPurchaseValue);

                    var flags = ResolveHeaderAlertFlags(poDetails);

                    var reportStatus = ResolveHeaderPrimaryStatus(
                        first.POIsActive,
                        first.POStatus,
                        poDetails,
                        ordered,
                        received,
                        accepted,
                        pendingQc,
                        rejected);

                    var isOverdue = flags.Contains("Overdue");
                    var hasQcFail = flags.Contains("HasQCFail");
                    var hasPendingQc = flags.Contains("WaitingQC");
                    var hasShortage = flags.Contains("Shortage");
                    var hasOpenBalance = !IsQuantityEnough(accepted, ordered);
                    var hasOverDelivery = flags.Contains("OverDelivered");

                    var needAction = IsNeedActionHeader(flags, reportStatus);

                    var deliveryDelayDays = poDetails.Count > 0
                        ? poDetails.Max(x => x.DeliveryDelayDays)
                        : 0;

                    var acceptanceDelayDays = poDetails.Count > 0
                        ? poDetails.Max(x => x.AcceptanceDelayDays)
                        : 0;

                    var lastReceiptDate = poDetails
                        .Where(x => x.LastReceiptDate.HasValue)
                        .Select(x => x.LastReceiptDate)
                        .Max();

                    return new PLPUPurchaseOrderHeaderReportDto
                    {
                        PurchaseOrderId = first.PurchaseOrderId,
                        POExternalId = first.POExternalId,
                        OrderType = first.OrderType,

                        SupplierId = first.SupplierId,
                        SupplierName = first.SupplierName,

                        CompanyId = first.CompanyId,
                        CreatedDate = first.CreatedDate,
                        CreatedBy = first.CreatedBy,

                        RequestDeliveryDate = first.HeaderRequestDeliveryDate,
                        RealDeliveryDate = first.RealDeliveryDate,
                        LastReceiptDate = lastReceiptDate,

                        POStatus = first.POStatus,
                        ReportStatus = reportStatus,
                        AlertFlags = string.Join(",", flags),

                        TotalLines = poDetails.Count,

                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = pendingQc,
                        RejectedQuantity = rejected,

                        RemainingToReceive = RemainingWithTolerance(ordered, received),
                        RemainingToAccept = RemainingWithTolerance(ordered, accepted),
                        OverReceivedQuantity = OverReceivedWithTolerance(received, ordered),

                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),

                        TotalPurchaseValue = totalValue,
                        ReceivedPurchaseValue = receivedValue,

                        IsOverdue = isOverdue,
                        DeliveryDelayDays = deliveryDelayDays,
                        AcceptanceDelayDays = acceptanceDelayDays,
                        DelayDays = deliveryDelayDays,

                        HasQCFail = hasQcFail,
                        HasPendingQC = hasPendingQc,
                        HasShortage = hasShortage,
                        HasOpenBalance = hasOpenBalance,
                        HasOverDelivery = hasOverDelivery,

                        NeedBuyerAction = needAction,
                        BuyerAction = ResolveBuyerAction(flags),

                        PLPUComment = first.PLPUComment,
                        Comment = first.Comment
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tính các chỉ số tổng hợp của báo cáo mua hàng.
        /// </summary>
        private static PLPUPurchaseOverviewSummaryDto BuildSummary(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            var ordered = details.Sum(x => x.OrderedQuantity);
            var received = details.Sum(x => x.WarehouseReceivedQuantity);
            var accepted = details.Sum(x => x.AcceptedQuantity);
            var pendingQc = details.Sum(x => x.PendingQcQuantity);
            var rejected = details.Sum(x => x.RejectedQuantity);

            var deliveryEvaluableDetails = details
                .Where(IsDeliveryOtdEvaluable)
                .ToList();

            var acceptanceEvaluableDetails = details
                .Where(IsAcceptanceOtdEvaluable)
                .ToList();

            return new PLPUPurchaseOverviewSummaryDto
            {
                TotalPO = headers.Count,
                ActivePO = headers.Count(x => !IsCancelledOrInactiveStatus(x.POStatus)),
                CancelledPO = headers.Count(x => x.ReportStatus == "Cancelled"),
                ClosedPO = headers.Count(x => x.ReportStatus == "Closed" || x.ReportStatus == "ClosedWithShortage"),
                CompletedPO = headers.Count(x => x.ReportStatus == "Completed"),

                NeedActionPO = headers.Count(x => x.NeedBuyerAction),
                OverduePO = headers.Count(x => x.IsOverdue),
                QCFailPO = headers.Count(x => x.HasQCFail),
                PendingQCPO = headers.Count(x => x.HasPendingQC),
                OverDeliveryPO = headers.Count(x => x.HasOverDelivery),

                TotalPurchaseValue = headers.Sum(x => x.TotalPurchaseValue),
                ReceivedPurchaseValue = headers.Sum(x => x.ReceivedPurchaseValue),

                OrderedQuantity = ordered,
                WarehouseReceivedQuantity = received,
                AcceptedQuantity = accepted,
                PendingQcQuantity = pendingQc,
                RejectedQuantity = rejected,

                RemainingToReceive = RemainingWithTolerance(ordered, received),
                RemainingToAccept = RemainingWithTolerance(ordered, accepted),
                OverReceivedQuantity = OverReceivedWithTolerance(received, ordered),

                CompletionPercent = Percent(accepted, ordered),
                WarehouseReceiptPercent = Percent(received, ordered),

                OnTimeDeliveryRate = Percent(
                    deliveryEvaluableDetails.Count(IsDeliveryOnTime),
                    deliveryEvaluableDetails.Count),

                OnTimeAcceptanceRate = Percent(
                    acceptanceEvaluableDetails.Count(IsAcceptanceOnTime),
                    acceptanceEvaluableDetails.Count),

                ShortageRate = Percent(
                    details.Where(x => HasFlag(x.AlertFlags, "Shortage")).Sum(x => x.RemainingToAccept),
                    ordered),

                OverDeliveryRate = Percent(details.Sum(x => x.OverReceivedQuantity), ordered),

                QcFailRate = Percent(rejected, received),
                QcAcceptanceRate = Percent(accepted, received),
                PendingQcRate = Percent(pendingQc, received),

                AverageDeliveryDelayDays = deliveryEvaluableDetails.Count > 0
                    ? Math.Round((decimal)deliveryEvaluableDetails.Average(x => x.DeliveryDelayDays), 2)
                    : 0,

                AverageAcceptanceDelayDays = acceptanceEvaluableDetails.Count > 0
                    ? Math.Round((decimal)acceptanceEvaluableDetails.Average(x => x.AcceptanceDelayDays), 2)
                    : 0,

                AverageDelayDays = deliveryEvaluableDetails.Count > 0
                    ? Math.Round((decimal)deliveryEvaluableDetails.Average(x => x.DeliveryDelayDays), 2)
                    : 0,

                MaxDeliveryDelayDays = details.Count > 0 ? details.Max(x => x.DeliveryDelayDays) : 0,
                MaxAcceptanceDelayDays = details.Count > 0 ? details.Max(x => x.AcceptanceDelayDays) : 0,
                MaxDelayDays = details.Count > 0 ? details.Max(x => x.DelayDays) : 0
            };
        }

        /// <summary>
        /// Tạo dữ liệu dashboard header gồm summary và các biểu đồ.
        /// </summary>
        private static PLPUPurchaseOrderHeaderDashboardDto BuildHeaderDashboard(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseOrderDetailReportDto> details,
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            var summary = BuildSummary(headers, details);
            var linesByDetailId = orderLines.ToDictionary(x => x.PurchaseOrderDetailId, x => x);

            return new PLPUPurchaseOrderHeaderDashboardDto
            {
                TotalPO = summary.TotalPO,
                ActivePO = summary.ActivePO,
                CancelledPO = summary.CancelledPO,
                ClosedPO = summary.ClosedPO,
                CompletedPO = summary.CompletedPO,
                NeedActionPO = summary.NeedActionPO,
                OverduePO = summary.OverduePO,
                QCFailPO = summary.QCFailPO,
                PendingQCPO = summary.PendingQCPO,
                OverDeliveryPO = summary.OverDeliveryPO,

                TotalPurchaseValue = summary.TotalPurchaseValue,
                ReceivedPurchaseValue = summary.ReceivedPurchaseValue,

                OrderedQuantity = summary.OrderedQuantity,
                WarehouseReceivedQuantity = summary.WarehouseReceivedQuantity,
                AcceptedQuantity = summary.AcceptedQuantity,
                PendingQcQuantity = summary.PendingQcQuantity,
                RejectedQuantity = summary.RejectedQuantity,

                RemainingToReceive = summary.RemainingToReceive,
                RemainingToAccept = summary.RemainingToAccept,
                OverReceivedQuantity = summary.OverReceivedQuantity,

                CompletionPercent = summary.CompletionPercent,
                WarehouseReceiptPercent = summary.WarehouseReceiptPercent,

                OnTimeDeliveryRate = summary.OnTimeDeliveryRate,
                OnTimeAcceptanceRate = summary.OnTimeAcceptanceRate,

                ShortageRate = summary.ShortageRate,
                OverDeliveryRate = summary.OverDeliveryRate,
                QcFailRate = summary.QcFailRate,
                QcAcceptanceRate = summary.QcAcceptanceRate,
                PendingQcRate = summary.PendingQcRate,

                AverageDeliveryDelayDays = summary.AverageDeliveryDelayDays,
                AverageAcceptanceDelayDays = summary.AverageAcceptanceDelayDays,
                AverageDelayDays = summary.AverageDelayDays,

                MaxDeliveryDelayDays = summary.MaxDeliveryDelayDays,
                MaxAcceptanceDelayDays = summary.MaxAcceptanceDelayDays,
                MaxDelayDays = summary.MaxDelayDays,

                PurchaseValueByMonth = BuildMonthChart(headers),
                QuantityByMonth = BuildMonthQuantityChart(headers),
                PurchaseValueBySupplier = BuildSupplierChart(headers, details),
                QuantityBySupplier = BuildSupplierQuantityChart(headers, details),
                QuantityByMaterial = BuildMaterialQuantityChart(details, linesByDetailId),
                QcResultBySupplier = BuildSupplierQcChart(headers, details),
                StatusDistribution = BuildStatusChart(headers),
                PriceVarianceByMaterial = BuildMaterialPriceVarianceChart(details, linesByDetailId)
            };
        }

        // ========================================== Các hàm chart ===========================================

        /// <summary>
        /// Tạo biểu đồ giá trị mua hàng theo tháng.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMonthChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return headers
                .Where(x => x.CreatedDate.HasValue)
                .GroupBy(x => new { x.CreatedDate!.Value.Year, x.CreatedDate!.Value.Month })
                .OrderBy(x => x.Key.Year)
                .ThenBy(x => x.Key.Month)
                .Select(g =>
                {
                    var start = new DateTime(g.Key.Year, g.Key.Month, 1);
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Label = start.ToString("yyyy-MM"),
                        PurchaseOrderCount = g.Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x => x.TotalPurchaseValue),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),
                        PeriodStart = start,
                        PeriodEnd = start.AddMonths(1).AddDays(-1)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ số lượng mua hàng theo tháng.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMonthQuantityChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return BuildMonthChart(headers)
                .Select(x =>
                {
                    x.TotalPurchaseValue = 0;
                    return x;
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ giá trị mua hàng theo nhà cung cấp.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildSupplierChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            var supplierByPo = headers.ToDictionary(x => x.PurchaseOrderId, x => x.SupplierId);
            var detailsBySupplier = details
                .GroupBy(x => supplierByPo.TryGetValue(x.PurchaseOrderId, out var supplierId)
                    ? supplierId ?? Guid.Empty
                    : Guid.Empty)
                .ToDictionary(x => x.Key, x => x.ToList());

            return headers
                .GroupBy(x => new { x.SupplierId, x.SupplierName })
                .OrderByDescending(x => x.Sum(r => r.TotalPurchaseValue))
                .Take(15)
                .Select(g =>
                {
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);
                    var supplierKey = g.Key.SupplierId ?? Guid.Empty;
                    var supplierDetails = detailsBySupplier.TryGetValue(supplierKey, out var supplierDetailRows)
                        ? supplierDetailRows
                        : new List<PLPUPurchaseOrderDetailReportDto>();
                    var deliveryEvaluable = supplierDetails.Where(IsDeliveryOtdEvaluable).ToList();
                    var acceptanceEvaluable = supplierDetails.Where(IsAcceptanceOtdEvaluable).ToList();

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Id = g.Key.SupplierId,
                        Label = string.IsNullOrWhiteSpace(g.Key.SupplierName)
                            ? "Unknown supplier"
                            : g.Key.SupplierName,
                        PurchaseOrderCount = g.Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x => x.TotalPurchaseValue),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),
                        QcFailRate = Percent(g.Sum(x => x.RejectedQuantity), received),
                        QcAcceptanceRate = Percent(accepted, received),
                        AverageDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,
                        AverageDeliveryDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,
                        AverageAcceptanceDelayDays = acceptanceEvaluable.Count > 0
                            ? Math.Round((decimal)acceptanceEvaluable.Average(x => x.AcceptanceDelayDays), 2)
                            : 0
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ số lượng mua hàng theo nhà cung cấp.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildSupplierQuantityChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            return BuildSupplierChart(headers, details)
                .OrderByDescending(x => x.OrderedQuantity)
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ QC theo nhà cung cấp.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildSupplierQcChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            return BuildSupplierChart(headers, details)
                .Where(x => x.WarehouseReceivedQuantity > 0 ||
                            x.PendingQcQuantity > 0 ||
                            x.RejectedQuantity > 0)
                .OrderByDescending(x => x.QcFailRate)
                .ThenByDescending(x => x.RejectedQuantity)
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ phân bố trạng thái PO.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildStatusChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return headers
                .GroupBy(x => x.ReportStatus)
                .OrderByDescending(x => x.Count())
                .Select(g =>
                {
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Label = ToVietnameseStatus(g.Key),
                        PurchaseOrderCount = g.Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x => x.TotalPurchaseValue),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ số lượng mua theo vật tư.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMaterialQuantityChart(
            List<PLPUPurchaseOrderDetailReportDto> details,
            Dictionary<Guid, PLPUPurchaseOrderLineRaw> linesByDetailId)
        {
            return details
                .Where(x => linesByDetailId.ContainsKey(x.PurchaseOrderDetailId))
                .GroupBy(x => new { x.MaterialId, x.MaterialCode, x.MaterialName })
                .OrderByDescending(x => x.Sum(r => r.OrderedQuantity))
                .Take(20)
                .Select(g =>
                {
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Id = g.Key.MaterialId,
                        Label = string.IsNullOrWhiteSpace(g.Key.MaterialCode)
                            ? g.Key.MaterialName
                            : $"{g.Key.MaterialCode} - {g.Key.MaterialName}",
                        LineCount = g.Count(),
                        PurchaseOrderCount = g.Select(x => x.PurchaseOrderId).Distinct().Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x =>
                            x.TotalPriceAgreed ??
                            x.OrderedQuantity * (x.UnitPriceAgreed ?? 0)),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),
                        QcFailRate = Percent(g.Sum(x => x.RejectedQuantity), received),
                        QcAcceptanceRate = Percent(accepted, received)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ chênh lệch giá theo vật tư.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMaterialPriceVarianceChart(
            List<PLPUPurchaseOrderDetailReportDto> details,
            Dictionary<Guid, PLPUPurchaseOrderLineRaw> linesByDetailId)
        {
            return details
                .Where(x => linesByDetailId.ContainsKey(x.PurchaseOrderDetailId) && x.PriceVariance.HasValue)
                .GroupBy(x => new { x.MaterialId, x.MaterialCode, x.MaterialName })
                .Select(g => new PLPUPurchaseReportChartPointDto
                {
                    Id = g.Key.MaterialId,
                    Label = string.IsNullOrWhiteSpace(g.Key.MaterialCode)
                        ? g.Key.MaterialName
                        : $"{g.Key.MaterialCode} - {g.Key.MaterialName}",
                    LineCount = g.Count(),
                    OrderedQuantity = g.Sum(x => x.OrderedQuantity),
                    TotalPurchaseValue = g.Sum(x =>
                        x.TotalPriceAgreed ??
                        x.OrderedQuantity * (x.UnitPriceAgreed ?? 0)),
                    ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                    PriceVarianceAmount = g.Sum(x => x.PriceImpactAmount ?? 0)
                })
                .OrderByDescending(x => Math.Abs(x.PriceVarianceAmount))
                .Take(20)
                .ToList();
        }

        // ========================================== Các hàm logic nhỏ ===========================================

        /// <summary>
        /// Tính hiệu suất nhà cung cấp dựa trên giao hàng, QC, thiếu hàng và giao vượt.
        /// </summary>
        private static List<PLPUSupplierPurchasePerformanceDto> BuildSupplierPerformance(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseOrderDetailReportDto> details,
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            var lineById = orderLines.ToDictionary(x => x.PurchaseOrderDetailId, x => x);
            var headerByPo = headers.ToDictionary(x => x.PurchaseOrderId, x => x);

            return details
                .Where(x => lineById.ContainsKey(x.PurchaseOrderDetailId))
                .GroupBy(x => lineById[x.PurchaseOrderDetailId].SupplierId)
                .Select(g =>
                {
                    var firstLine = lineById[g.First().PurchaseOrderDetailId];

                    var poIds = g.Select(x => x.PurchaseOrderId).Distinct().ToList();

                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);
                    var rejected = g.Sum(x => x.RejectedQuantity);
                    var pending = g.Sum(x => x.PendingQcQuantity);

                    var totalValue = g.Sum(x =>
                        x.TotalPriceAgreed ??
                        x.OrderedQuantity * (x.UnitPriceAgreed ?? 0));

                    var receivedValue = g.Sum(x => x.ReceivedPurchaseValue);
                    var priceVarianceAmount = g.Sum(x => x.PriceImpactAmount ?? 0);

                    var deliveryEvaluable = g.Where(IsDeliveryOtdEvaluable).ToList();
                    var acceptanceEvaluable = g.Where(IsAcceptanceOtdEvaluable).ToList();

                    var riskReasons = BuildSupplierRiskReasons(g.ToList(), headerByPo);

                    var supplierDeliveryOtd = Percent(
                        deliveryEvaluable.Count(IsDeliveryOnTime),
                        deliveryEvaluable.Count);

                    var supplierAcceptanceOtd = Percent(
                        acceptanceEvaluable.Count(IsAcceptanceOnTime),
                        acceptanceEvaluable.Count);

                    return new PLPUSupplierPurchasePerformanceDto
                    {
                        SupplierId = firstLine.SupplierId,
                        SupplierName = firstLine.SupplierName,

                        TotalPO = poIds.Count,
                        TotalLines = g.Count(),

                        TotalPurchaseValue = totalValue,
                        ReceivedPurchaseValue = receivedValue,

                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = pending,
                        RejectedQuantity = rejected,

                        SupplierOTD = supplierDeliveryOtd,
                        SupplierDeliveryOTD = supplierDeliveryOtd,
                        SupplierAcceptanceOTD = supplierAcceptanceOtd,

                        SupplierFillRate = Percent(g.Sum(x => Math.Min(x.WarehouseReceivedQuantity, x.OrderedQuantity)), ordered),
                        SupplierAcceptanceRate = Percent(accepted, received),
                        SupplierQcFailRate = Percent(rejected, received),

                        AverageDeliveryDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,

                        AverageAcceptanceDelayDays = acceptanceEvaluable.Count > 0
                            ? Math.Round((decimal)acceptanceEvaluable.Average(x => x.AcceptanceDelayDays), 2)
                            : 0,

                        AverageDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,

                        AverageUnitPrice = ordered == 0
                            ? 0
                            : Math.Round(totalValue / ordered, 4),

                        PriceVarianceAmount = priceVarianceAmount,

                        RiskScore = riskReasons.Sum(x => x.ImpactScore),
                        RiskReasons = riskReasons
                    };
                })
                .ToList();
        }

        private static List<PLPUSupplierRiskReasonDto> BuildSupplierRiskReasons(
            List<PLPUPurchaseOrderDetailReportDto> details,
            Dictionary<Guid, PLPUPurchaseOrderHeaderReportDto> headerByPo)
        {
            var reasons = new List<PLPUSupplierRiskReasonDto>();

            foreach (var poGroup in details.GroupBy(x => x.PurchaseOrderId))
            {
                if (!headerByPo.TryGetValue(poGroup.Key, out var header) || !header.IsOverdue)
                    continue;

                var detail = poGroup
                    .OrderByDescending(x => x.DeliveryDelayDays)
                    .ThenByDescending(x => x.RemainingToReceive)
                    .FirstOrDefault();

                reasons.Add(new PLPUSupplierRiskReasonDto
                {
                    PurchaseOrderId = header.PurchaseOrderId,
                    POExternalId = header.POExternalId,
                    PurchaseOrderDetailId = detail?.PurchaseOrderDetailId,
                    LineNo = detail?.LineNo,
                    MaterialCode = detail?.MaterialCode ?? string.Empty,
                    MaterialName = detail?.MaterialName ?? string.Empty,
                    ReasonCode = "Overdue",
                    ReasonText = "PO quá hạn giao theo lượng nhập kho",
                    ImpactScore = 30,
                    OrderedQuantity = detail?.OrderedQuantity ?? header.OrderedQuantity,
                    WarehouseReceivedQuantity = detail?.WarehouseReceivedQuantity ?? header.WarehouseReceivedQuantity,
                    AcceptedQuantity = detail?.AcceptedQuantity ?? header.AcceptedQuantity,
                    PendingQcQuantity = detail?.PendingQcQuantity ?? header.PendingQcQuantity,
                    RejectedQuantity = detail?.RejectedQuantity ?? header.RejectedQuantity,
                    DelayDays = header.DeliveryDelayDays,
                    RequestDeliveryDate = header.RequestDeliveryDate,
                    ActualDate = detail?.DateWhenReceivedEnough ?? detail?.LastReceiptDate
                });
            }

            foreach (var detail in details)
            {
                if (detail.RejectedQuantity > 0)
                    reasons.Add(CreateRiskReason(detail, "HasQCFail", "Có hàng QC fail", 25, detail.RejectedQuantity, detail.LastReceiptDate));

                if (HasFlag(detail.AlertFlags, "Shortage"))
                    reasons.Add(CreateRiskReason(detail, "Shortage", "Thiếu hàng so với số lượng đặt", 20, detail.RemainingToReceive, detail.LastReceiptDate));

                if (detail.PendingQcQuantity > 0)
                    reasons.Add(CreateRiskReason(detail, "WaitingQC", "Có hàng nhập kho đang chờ QC", 10, detail.PendingQcQuantity, detail.LastReceiptDate));

                if (detail.OverReceivedQuantity > 0)
                    reasons.Add(CreateRiskReason(detail, "OverDelivered", "Giao vượt số lượng đặt vượt ngưỡng cho phép", 5, detail.OverReceivedQuantity, detail.LastReceiptDate));
            }

            return reasons
                .OrderByDescending(x => x.ImpactScore)
                .ThenByDescending(x => x.DelayDays)
                .ThenBy(x => x.POExternalId)
                .ThenBy(x => x.LineNo ?? int.MaxValue)
                .ToList();
        }

        private static PLPUSupplierRiskReasonDto CreateRiskReason(
            PLPUPurchaseOrderDetailReportDto detail,
            string reasonCode,
            string reasonText,
            decimal impactScore,
            decimal quantity,
            DateTime? actualDate)
        {
            return new PLPUSupplierRiskReasonDto
            {
                PurchaseOrderId = detail.PurchaseOrderId,
                POExternalId = detail.POExternalId,
                PurchaseOrderDetailId = detail.PurchaseOrderDetailId,
                LineNo = detail.LineNo,
                MaterialCode = detail.MaterialCode,
                MaterialName = detail.MaterialName,
                ReasonCode = reasonCode,
                ReasonText = reasonText,
                ImpactScore = impactScore,
                OrderedQuantity = detail.OrderedQuantity,
                WarehouseReceivedQuantity = detail.WarehouseReceivedQuantity,
                AcceptedQuantity = detail.AcceptedQuantity,
                PendingQcQuantity = detail.PendingQcQuantity,
                RejectedQuantity = detail.RejectedQuantity,
                DelayDays = detail.DeliveryDelayDays,
                RequestDeliveryDate = detail.RequestDeliveryDate,
                ActualDate = actualDate
            };
        }

        /// <summary>
        /// Tạo danh sách dòng QC từ các phiếu nhập đã phân bổ.
        /// </summary>
        private static List<PLPUPurchaseQcReportDto> BuildQcRowsFromAllocatedReceipts(
            List<PLPUPurchaseReceiptReportDto> receipts)
        {
            return receipts
                .Select(receipt =>
                {
                    var qcGroup = !string.IsNullOrWhiteSpace(receipt.QCGroup)
                        ? receipt.QCGroup
                        : receipt.QCResult is HRM.Domain.Enums.Devandqa.QcDecision.QCPass
                            or HRM.Domain.Enums.Devandqa.QcDecision.Special
                                ? "Accepted"
                                : receipt.QCResult == HRM.Domain.Enums.Devandqa.QcDecision.QCFail
                                    ? "Rejected"
                                    : "Pending";

                    return new PLPUPurchaseQcReportDto
                    {
                        QCInputByQCId = null,
                        VoucherDetailId = receipt.WarehouseVoucherDetailId,

                        PurchaseOrderId = receipt.PurchaseOrderId,
                        PurchaseOrderDetailId = receipt.PurchaseOrderDetailId,

                        POExternalId = receipt.POExternalId,
                        LineNo = receipt.LineNo,

                        SupplierId = receipt.SupplierId,
                        SupplierName = receipt.SupplierName,

                        MaterialCode = receipt.ProductCode,
                        MaterialName = receipt.ProductName,
                        LotNumber = receipt.LotNumber,

                        QtyKg = receipt.QtyKg,

                        QCResult = receipt.QCResult,
                        QCGroup = qcGroup,

                        ReceiptDate = receipt.ReceiptDate,
                        QCCreatedDate = receipt.QCCreatedDate,

                        QCAgeDays = qcGroup == "Pending"
                            ? Math.Max((DateTime.Today - receipt.ReceiptDate.Date).Days, 0)
                            : 0,

                        BuyerAction = qcGroup == "Rejected" ? "Handle QC fail with supplier"
                            : qcGroup == "Pending" ? "Follow up QC decision"
                            : string.Empty
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tìm ngày mà số lượng cộng dồn đã đủ theo số lượng đặt.
        /// </summary>
        private static DateTime? GetDateWhenEnough(
            IEnumerable<PLPUPurchaseReceiptReportDto> receipts,
            decimal orderedQuantity,
            Func<PLPUPurchaseReceiptReportDto, decimal> quantitySelector)
        {
            if (orderedQuantity <= 0)
                return null;

            decimal running = 0;

            foreach (var receipt in receipts.OrderBy(x => x.ReceiptDate))
            {
                running += quantitySelector(receipt);

                if (running >= orderedQuantity)
                    return receipt.ReceiptDate;
            }

            return null;
        }

        /// <summary>
        /// Tính số ngày trễ so với ngày yêu cầu.
        /// </summary>
        private static int CalculateDelayDays(
            DateTime? dueDate,
            DateTime? actualEnoughDate,
            decimal currentQuantity,
            decimal orderedQuantity)
        {
            if (!dueDate.HasValue || orderedQuantity <= 0)
                return 0;

            var comparisonDate = IsQuantityEnough(currentQuantity, orderedQuantity) && actualEnoughDate.HasValue
                ? actualEnoughDate.Value.Date
                : DateTime.Today;

            if (actualEnoughDate.HasValue && IsBeforeNextDay(actualEnoughDate.Value, dueDate.Value))
                return 0;

            return Math.Max((comparisonDate - dueDate.Value.Date).Days, 0);
        }

        /// <summary>
        /// Xác định trạng thái chính của một dòng PO.
        /// </summary>
        private static string ResolveLinePrimaryStatus(
            bool isActive,
            string poStatus,
            decimal ordered,
            decimal received,
            decimal accepted,
            decimal pendingQc,
            decimal rejected,
            DateTime? dueDate)
        {
            if (!isActive || IsCancelledOrInactiveStatus(poStatus))
                return "Cancelled";

            if (IsClosedStatus(poStatus))
            {
                if (ordered > 0 && !IsQuantityEnough(accepted, ordered))
                    return "ClosedWithShortage";

                return "Closed";
            }

            if (ordered <= 0)
                return "NoQuantity";

            if (IsQuantityEnough(accepted, ordered))
                return "AcceptedEnough";

            if (IsQuantityEnough(received, ordered) && pendingQc > 0)
                return "DeliveredEnoughWaitingQC";

            if (IsQuantityEnough(received, ordered))
                return "DeliveredEnough";

            if (accepted > 0)
                return "PartiallyAccepted";

            if (received > 0)
                return "PartiallyDelivered";

            if (dueDate.HasValue && DateTime.Today > dueDate.Value.Date)
                return "OverdueNotDelivered";

            return "NotDelivered";
        }

        /// <summary>
        /// Xác định các cảnh báo của một dòng PO.
        /// </summary>
        private static List<string> ResolveLineAlertFlags(
            decimal ordered,
            decimal received,
            decimal accepted,
            decimal pendingQc,
            decimal rejected,
            DateTime? dueDate,
            DateTime? dateWhenReceivedEnough)
        {
            var flags = new List<string>();

            if (ordered <= 0)
                return flags;

            if (dueDate.HasValue &&
                ((DateTime.Today > dueDate.Value.Date && !IsQuantityEnough(received, ordered)) ||
                 (dateWhenReceivedEnough.HasValue && dateWhenReceivedEnough.Value.Date > dueDate.Value.Date)))
            {
                flags.Add("Overdue");

                if (!IsQuantityEnough(received, ordered))
                    flags.Add("Shortage");
            }

            if (rejected > 0)
                flags.Add("HasQCFail");

            if (pendingQc > 0)
                flags.Add("WaitingQC");

            if (IsOverDelivered(received, ordered))
                flags.Add("OverDelivered");

            return flags.Distinct().ToList();
        }

        /// <summary>
        /// Gom cảnh báo từ các dòng chi tiết lên header PO.
        /// </summary>
        private static List<string> ResolveHeaderAlertFlags(
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            return details
                .SelectMany(x => SplitFlags(x.AlertFlags))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Xác định trạng thái chính của header PO.
        /// </summary>
        private static string ResolveHeaderPrimaryStatus(
            bool isActive,
            string poStatus,
            List<PLPUPurchaseOrderDetailReportDto> details,
            decimal ordered,
            decimal received,
            decimal accepted,
            decimal pendingQc,
            decimal rejected)
        {
            if (!isActive || IsCancelledOrInactiveStatus(poStatus))
                return "Cancelled";

            if (IsClosedStatus(poStatus))
            {
                if (ordered > 0 && !IsQuantityEnough(accepted, ordered))
                    return "ClosedWithShortage";

                return "Closed";
            }

            if (ordered <= 0 || details.Count == 0)
                return "NoQuantity";

            if (IsQuantityEnough(accepted, ordered))
                return "Completed";

            if (details.Any(x => HasFlag(x.AlertFlags, "Overdue")))
                return "OverdueWithShortage";

            if (rejected > 0)
                return "HasQCFail";

            if (pendingQc > 0 && IsQuantityEnough(received, ordered))
                return "DeliveredEnoughWaitingQC";

            if (pendingQc > 0)
                return "WaitingQC";

            if (IsQuantityEnough(received, ordered))
                return "DeliveredEnough";

            if (accepted > 0)
                return "PartiallyAccepted";

            if (received > 0)
                return "PartiallyDelivered";

            return "NotDelivered";
        }

        /// <summary>
        /// Kiểm tra PO có cần buyer xử lý không.
        /// </summary>
        private static bool IsNeedActionHeader(List<string> flags, string reportStatus)
        {
            if (reportStatus is "Cancelled" or "Closed" or "Completed")
                return false;

            return flags.Contains("Overdue") ||
                   flags.Contains("Shortage") ||
                   flags.Contains("HasQCFail") ||
                   flags.Contains("WaitingQC") ||
                   flags.Contains("OverDelivered") ||
                   reportStatus == "ClosedWithShortage";
        }

        /// <summary>
        /// Kiểm tra dòng PO có cần buyer xử lý không.
        /// </summary>
        private static bool IsNeedActionLine(List<string> flags)
        {
            return flags.Contains("Overdue") ||
                   flags.Contains("Shortage") ||
                   flags.Contains("HasQCFail") ||
                   flags.Contains("WaitingQC") ||
                   flags.Contains("OverDelivered");
        }

        /// <summary>
        /// Đề xuất hành động buyer cần làm dựa trên cảnh báo.
        /// </summary>
        private static string ResolveBuyerAction(IEnumerable<string> flags)
        {
            var flagSet = flags.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var actions = new List<string>();

            if (flagSet.Contains("Overdue") || flagSet.Contains("Shortage"))
                actions.Add("Follow up supplier for overdue shortage");

            if (flagSet.Contains("HasQCFail"))
                actions.Add("Handle QC fail with supplier");

            if (flagSet.Contains("WaitingQC"))
                actions.Add("Follow up QC decision");

            if (flagSet.Contains("OverDelivered"))
                actions.Add("Review over delivery");

            return string.Join("; ", actions);
        }

        /// <summary>
        /// Nếu là vật tư đóng gói và PO được tạo trước ngày cutoff,
        /// tự động xem số lượng QC đạt bằng số lượng nhập kho.
        /// </summary>
        private static bool IsAutoAcceptPackagingQc(PLPUPurchaseOrderLineRaw line)
        {

            var result =
                line.CategoryId == MaterialPackagingCategoryId
                && line.CreatedDate.HasValue;

            return result;
        }
        private static bool IsDeliveryOtdEvaluable(PLPUPurchaseOrderDetailReportDto detail)
        {
            if (detail.OrderedQuantity <= 0 || !detail.RequestDeliveryDate.HasValue)
                return false;

            return detail.DateWhenReceivedEnough.HasValue;
        }

        private static bool IsAcceptanceOtdEvaluable(PLPUPurchaseOrderDetailReportDto detail)
        {
            if (detail.OrderedQuantity <= 0 || !detail.RequestDeliveryDate.HasValue)
                return false;

            return detail.DateWhenAcceptedEnough.HasValue;
        }

        private static bool IsDeliveryOnTime(PLPUPurchaseOrderDetailReportDto detail)
        {
            return detail.DateWhenReceivedEnough.HasValue &&
                   detail.RequestDeliveryDate.HasValue &&
                   IsBeforeNextDay(detail.DateWhenReceivedEnough.Value, detail.RequestDeliveryDate.Value);
        }

        private static bool IsAcceptanceOnTime(PLPUPurchaseOrderDetailReportDto detail)
        {
            return detail.DateWhenAcceptedEnough.HasValue &&
                   detail.RequestDeliveryDate.HasValue &&
                   IsBeforeNextDay(detail.DateWhenAcceptedEnough.Value, detail.RequestDeliveryDate.Value);
        }

        private static bool IsBeforeNextDay(DateTime actualDate, DateTime dueDate)
        {
            return actualDate < dueDate.Date.AddDays(1);
        }

        private static bool IsOverDelivered(decimal received, decimal ordered)
        {
            if (ordered <= 0)
                return received > 0;

            var toleranceQty = ordered * OverDeliveryTolerancePercent / 100m;

            return received > ordered + toleranceQty;
        }

        private static bool IsCancelledOrInactiveStatus(string? status)
        {
            status ??= string.Empty;

            return status.Equals(StatusCancelled, StringComparison.OrdinalIgnoreCase) ||
                   status.Equals(StatusCanceled, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsClosedStatus(string? status)
        {
            status ??= string.Empty;

            return status.Equals(StatusClosed, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasFlag(string flags, string flag)
        {
            return SplitFlags(flags).Contains(flag, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SplitFlags(string flags)
        {
            if (string.IsNullOrWhiteSpace(flags))
                return Enumerable.Empty<string>();

            return flags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string ReceiptMatchKey(string poExternalId, string materialCode)
        {
            return $"{NormalizeKey(poExternalId)}|{NormalizeKey(materialCode)}";
        }

        private static string NormalizeKey(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static decimal Positive(decimal value)
        {
            return value > 0 ? value : 0;
        }

        private static decimal ResolveUnitPrice(
            decimal orderedQuantity,
            decimal? unitPriceAgreed,
            decimal? totalPriceAgreed,
            decimal? baseCostSnapshot)
        {
            if (unitPriceAgreed.HasValue)
                return unitPriceAgreed.Value;

            if (totalPriceAgreed.HasValue && orderedQuantity > 0)
                return totalPriceAgreed.Value / orderedQuantity;

            return baseCostSnapshot ?? 0;
        }

        private static decimal Percent(decimal numerator, decimal denominator)
        {
            return denominator == 0 ? 0 : Math.Round(numerator / denominator * 100, 2);
        }

        private static decimal Percent(int numerator, int denominator)
        {
            return denominator == 0 ? 0 : Math.Round((decimal)numerator / denominator * 100, 2);
        }

        private static int NormalizePageNumber(int pageNumber)
        {
            return pageNumber <= 0 ? 1 : pageNumber;
        }

        private static int NormalizePageSize(int pageSize)
        {
            return pageSize <= 0 ? 15 : pageSize;
        }

        private static PLPUPagedResult<T> ToPagedResult<T>(List<T> items, int pageNumber, int pageSize)
        {
            var pagedItems = items
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PLPUPagedResult<T>(pagedItems, items.Count, pageNumber, pageSize);
        }


        private static decimal LowerCompletionBound(decimal ordered)
        {
            if (ordered <= 0)
                return 0;

            return ordered * (100m - CompletionTolerancePercent) / 100m;
        }

        private static decimal UpperCompletionBound(decimal ordered)
        {
            if (ordered <= 0)
                return 0;

            return ordered * (100m + CompletionTolerancePercent) / 100m;
        }

        private static bool IsQuantityEnough(decimal actual, decimal ordered)
        {
            if (ordered <= 0)
                return actual <= 0;

            return actual >= LowerCompletionBound(ordered);
        }

        private static decimal RemainingWithTolerance(decimal ordered, decimal actual)
        {
            return IsQuantityEnough(actual, ordered)
                ? 0
                : Positive(ordered - actual);
        }

        private static decimal OverReceivedWithTolerance(decimal received, decimal ordered)
        {
            return IsOverDelivered(received, ordered)
                ? Positive(received - ordered)
                : 0;
        }

        private static string ToVietnameseStatus(string? status)
        {
            return status switch
            {
                "Cancelled" => "Đã hủy",
                "Closed" => "Đã đóng",
                "ClosedWithShortage" => "Đã đóng còn thiếu",
                "NoQuantity" => "Không có số lượng",
                "Completed" => "Hoàn thành",
                "AcceptedEnough" => "QC đạt đủ",
                "DeliveredEnoughWaitingQC" => "Đã giao đủ, chờ QC",
                "DeliveredEnough" => "Đã giao đủ",
                "OverdueWithShortage" => "Quá hạn còn thiếu",
                "HasQCFail" => "Có hàng lỗi QC",
                "WaitingQC" => "Chờ QC",
                "PartiallyAccepted" => "QC đạt một phần",
                "PartiallyDelivered" => "Giao một phần",
                "OverdueNotDelivered" => "Quá hạn chưa giao",
                "NotDelivered" => "Chưa giao",
                _ => status ?? string.Empty
            };
        }

        private static string ToVietnameseFlag(string? flag)
        {
            return flag switch
            {
                "Overdue" => "Quá hạn",
                "Shortage" => "Thiếu hàng",
                "HasQCFail" => "Có hàng lỗi QC",
                "WaitingQC" => "Chờ QC",
                "OverDelivered" => "Giao vượt",
                _ => flag ?? string.Empty
            };
        }

        private static string ToVietnameseFlags(string? flags)
        {
            if (string.IsNullOrWhiteSpace(flags))
                return string.Empty;

            return string.Join(", ",
                flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(ToVietnameseFlag));
        }

        private static string ToVietnameseBuyerAction(string? action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return string.Empty;

            var parts = action.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var translated = parts.Select(x => x switch
            {
                "Follow up supplier for overdue shortage" => "Theo dõi NCC do quá hạn/thiếu hàng",
                "Handle QC fail with supplier" => "Xử lý hàng lỗi QC với NCC",
                "Follow up QC decision" => "Theo dõi kết quả QC",
                "Review over delivery" => "Kiểm tra giao vượt",
                _ => x
            });

            return string.Join("; ", translated);
        }

        private static string ToVietnameseQcGroup(string? qcGroup)
        {
            return qcGroup switch
            {
                "Accepted" => "Đạt QC",
                "Rejected" => "Không đạt QC",
                "Pending" => "Chờ QC",
                _ => qcGroup ?? string.Empty
            };
        }

        private static List<PLPUPurchaseOrderHeaderReportDto> LocalizeHeaders(
            IEnumerable<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return headers.Select(x =>
            {
                x.ReportStatus = ToVietnameseStatus(x.ReportStatus);
                x.AlertFlags = ToVietnameseFlags(x.AlertFlags);
                x.BuyerAction = ToVietnameseBuyerAction(x.BuyerAction);
                return x;
            }).ToList();
        }

        private static List<PLPUPurchaseOrderDetailReportDto> LocalizeDetails(
            IEnumerable<PLPUPurchaseOrderDetailReportDto> details)
        {
            return details.Select(x =>
            {
                x.LineReportStatus = ToVietnameseStatus(x.LineReportStatus);
                x.AlertFlags = ToVietnameseFlags(x.AlertFlags);
                x.BuyerAction = ToVietnameseBuyerAction(x.BuyerAction);
                return x;
            }).ToList();
        }

        private static List<PLPUPurchaseReceiptReportDto> LocalizeReceipts(
            IEnumerable<PLPUPurchaseReceiptReportDto> receipts)
        {
            return receipts.Select(x =>
            {
                x.QCGroup = ToVietnameseQcGroup(x.QCGroup);
                return x;
            }).ToList();
        }

        private static List<PLPUPurchaseQcReportDto> LocalizeQcRows(
            IEnumerable<PLPUPurchaseQcReportDto> rows)
        {
            return rows.Select(x =>
            {
                x.QCGroup = ToVietnameseQcGroup(x.QCGroup);
                x.BuyerAction = ToVietnameseBuyerAction(x.BuyerAction);
                return x;
            }).ToList();
        }
    }

    internal static class PLPUPurchaseReceiptReportDtoExtensions
    {
        public static string MaterialCode(this PLPUPurchaseReceiptReportDto row)
        {
            return row.ProductCode;
        }
    }


}
