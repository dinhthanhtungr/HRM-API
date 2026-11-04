using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
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
            Guid companyId,
            CancellationToken ct = default)
        {
            var query = new PLPUPurchaseOverviewQuery
            {
                PurchaseOrderId = purchaseOrderId,
                CompanyId = companyId,
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

}
