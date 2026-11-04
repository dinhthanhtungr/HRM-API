namespace HRM.Application.Features.Reports.PLPU;
public partial class PLPUPurchaseOverviewReportService
{
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

}
