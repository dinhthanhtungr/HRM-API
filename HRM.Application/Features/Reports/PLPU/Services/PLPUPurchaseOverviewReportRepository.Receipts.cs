using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.Reports.PLPU;
public partial class PLPUPurchaseOverviewReportRepository
{
        private IQueryable<PLPUPurchaseReceiptReportDto> BuildReceiptQuery(
            PLPUPurchaseOverviewQuery query, bool byMovementDate = false, bool supplierReturns = false,
            bool specialDecisions = false)
        {
            var fromDate = query.From?.Date;
            var toDate = query.To?.Date.AddDays(1);
            var keyword = query.Keyword?.Trim();

            var q =
                from po in _context.PurchaseOrders.AsNoTracking()
                join s in _context.Suppliers.AsNoTracking().Where(x => x.CompanyId == query.CompanyId)
                    on po.SupplierId equals s.SupplierId into sJoin
                from s in sJoin.DefaultIfEmpty()
                join wr in _context.WarehouseRequests.AsNoTracking()
                    on po.ExternalId equals wr.codeFromRequest
                join v in _context.WarehouseVouchers.AsNoTracking()
                    on wr.RequestId equals v.RequestId
                join vd in _context.WarehouseVoucherDetails.AsNoTracking()
                    on v.VoucherId equals vd.VoucherId
                join ledger in _context.WarehouseShelfLedgers.AsNoTracking()
                    on vd.VoucherDetailId equals ledger.VoucherDetailId
                where wr.CompanyId == query.CompanyId && v.CompanyId == query.CompanyId && ledger.CompanyId == query.CompanyId
                      && (supplierReturns
                          ? wr.ReqType == WareHouseRequestType.ExportReturn
                            && v.VoucherType == (int)WareHouseRequestType.ExportReturn
                            && !vd.IsIncrease && ledger.StockType == StockType.DefectiveRawMaterial
                          : (PurchaseReceiptVoucherTypes.Contains(v.VoucherType)
                             || byMovementDate && v.VoucherType == (int)WareHouseRequestType.ImportDefectiveRawMaterial)
                            && ledger.DeltaKg > 0)
                let qcLatest = byMovementDate && !specialDecisions ? null : _context.QCInputByQCs.AsNoTracking()
                    .Where(qc => qc.VoucherDetailId == vd.VoucherDetailId)
                    .OrderByDescending(qc => qc.CreatedDate)
                    .ThenByDescending(qc => qc.QCInputByQCId)
                    .Select(qc => new
                    {
                        qc.QCInputByQCId,
                        qc.ImportWarehouseType,
                        qc.CreatedDate
                    })
                    .FirstOrDefault()
                select new { po, s, wr, v, vd, ledger, qcLatest };

            q = q.Where(x => (x.po.OrderType ?? "") == SupplierOrderType);

            if (byMovementDate)
            {
                q = q.Where(x => (x.ledger.ProductCode ?? x.vd.ProductCode) != null &&
                    _context.PurchaseOrderDetails.Any(line =>
                    line.PurchaseOrderId == x.po.PurchaseOrderId &&
                    line.Material.CompanyId == query.CompanyId &&
                    line.Material.Category.CompanyId == query.CompanyId &&
                    line.Material.Category.Types == "NVL" &&
                    (line.MaterialExternalIDSnapshot ?? line.Material.ExternalId) ==
                        (x.ledger.ProductCode ?? x.vd.ProductCode)));
            }
            else
            {
                q = q.Where(x =>
                    EF.Functions.ILike(
                        x.ledger.ProductCode ?? x.vd.ProductCode ?? "",
                        IncludedMaterialCodePrefix + "%"));

                foreach (var excludedKeyword in ExcludedMaterialCodeKeywords)
                {
                    var pattern = "%" + excludedKeyword + "%";
                    q = q.Where(x => !EF.Functions.ILike(
                        x.ledger.ProductCode ?? x.vd.ProductCode ?? "", pattern));
                }
            }

            q = q.Where(x =>
                string.IsNullOrEmpty(x.po.ExternalId) ||
                !EF.Functions.ILike(x.po.ExternalId, "PO%"));

            if (!query.IncludeInactive)
                q = q.Where(x => x.po.IsActive == true && x.wr.IsActive);

            if (byMovementDate)
            {
                q = q.Where(x => !x.ledger.VoucherId.HasValue || x.ledger.VoucherId == x.v.VoucherId);
                q = q.Where(x => !_context.PurchaseOrders.Any(other => other.CompanyId == x.po.CompanyId
                    && other.ExternalId == x.po.ExternalId && other.PurchaseOrderId != x.po.PurchaseOrderId));
                q = specialDecisions
                    ? q.Where(x => _context.QCInputByQCs.Any(qc => qc.VoucherDetailId == x.vd.VoucherDetailId
                        && qc.ImportWarehouseType == QcDecision.Special
                        && qc.CreatedDate >= fromDate && qc.CreatedDate < toDate
                        && qc.QCInputByQCId == x.qcLatest!.QCInputByQCId))
                    : q.Where(x => x.ledger.CreatedAt >= fromDate && x.ledger.CreatedAt < toDate);
            }
            else if (fromDate.HasValue)
                q = q.Where(x => x.po.CreateDate >= fromDate.Value);

            if (!byMovementDate && toDate.HasValue)
                q = q.Where(x => x.po.CreateDate < toDate.Value);

            if (query.CompanyId.HasValue)
                q = q.Where(x => x.po.CompanyId == query.CompanyId.Value);

            if (query.PurchaseOrderId.HasValue)
                q = q.Where(x => x.po.PurchaseOrderId == query.PurchaseOrderId.Value);

            if (query.SupplierId.HasValue)
                q = q.Where(x => x.po.SupplierId == query.SupplierId.Value);

            if (query.MaterialId.HasValue)
            {
                q = q.Where(x =>
                    _context.PurchaseOrderDetails.AsNoTracking().Any(pod =>
                        pod.PurchaseOrderId == x.po.PurchaseOrderId &&
                        pod.IsActive &&
                        pod.MaterialId == query.MaterialId.Value &&
                        (pod.MaterialExternalIDSnapshot ?? "") ==
                        (x.ledger.ProductCode ?? x.vd.ProductCode ?? "")
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(query.OrderType))
                q = q.Where(x => (x.po.OrderType ?? "") == query.OrderType);

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(x => (x.po.Status ?? "") == query.Status);

            var search = PLPUSmartSearch.Build(q, x => x.po.ExternalId, x => x.s != null ? x.s.ExternalId : "",
                x => x.s != null ? x.s.SupplierName : "", x => x.ledger.ProductCode ?? x.vd.ProductCode, x => x.vd.ProductName);
            q = search.ApplyRequest(q, keyword, query, out keyword);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                q = q.Where(x =>
                    (x.po.ExternalId ?? "").Contains(keyword!) ||
                    (x.ledger.ProductCode ?? x.vd.ProductCode ?? "").Contains(keyword!) ||
                    (x.vd.ProductName ?? "").Contains(keyword!) ||
                    (x.ledger.LotNumber ?? x.vd.LotNumber ?? "").Contains(keyword!) ||
                    (x.s != null && (x.s.SupplierName ?? "").Contains(keyword!)) ||
                    (x.s != null && (x.s.ExternalId ?? "").Contains(keyword!))
                );
            }

            return q.Select(x => new PLPUPurchaseReceiptReportDto
            {
                WarehouseRequestId = x.wr.RequestId,
                CodeFromRequest = x.wr.codeFromRequest ?? string.Empty,

                WarehouseVoucherId = x.v.VoucherId,
                WarehouseVoucherCode = x.v.VoucherCode ?? string.Empty,

                WarehouseVoucherDetailId = x.ledger.VoucherDetailId ?? x.vd.VoucherDetailId,
                VoucherType = x.v.VoucherType,

                ReceiptDate = x.ledger.CreatedAt,

                PurchaseOrderId = x.po.PurchaseOrderId,
                PurchaseOrderDetailId = null,

                POExternalId = x.po.ExternalId ?? string.Empty,
                LineNo = 0,

                SupplierId = x.po.SupplierId,
                SupplierName = x.s != null ? (x.s.SupplierName ?? string.Empty) : string.Empty,

                ProductCode = x.ledger.ProductCode ?? x.vd.ProductCode ?? string.Empty,
                ProductName = x.vd.ProductName ?? string.Empty,
                LotNumber = x.ledger.LotNumber ?? x.vd.LotNumber ?? string.Empty,

                QtyKg = x.ledger.DeltaKg,

                QCResult = x.qcLatest != null ? x.qcLatest.ImportWarehouseType : null,
                QCCreatedDate = x.qcLatest != null ? x.qcLatest.CreatedDate : null,

                VoucherDetailType = x.vd.VoucherType
            });
        }


}
