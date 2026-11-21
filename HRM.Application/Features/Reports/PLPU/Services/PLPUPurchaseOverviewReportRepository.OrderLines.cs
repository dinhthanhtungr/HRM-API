using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.Reports.PLPU;
public partial class PLPUPurchaseOverviewReportRepository
{
        private IQueryable<PLPUPurchaseOrderLineRaw> BuildOrderLineQuery(PLPUPurchaseOverviewQuery query)
        {
            var fromDate = query.From?.Date;
            var toDate = query.To?.Date.AddDays(1);
            var keyword = query.Keyword?.Trim();

            var q =
                from po in _context.PurchaseOrders.AsNoTracking()
                join pod in _context.PurchaseOrderDetails.AsNoTracking()
                    on po.PurchaseOrderId equals pod.PurchaseOrderId
                join m in _context.Materials.AsNoTracking().Where(x => x.CompanyId == query.CompanyId)
                    on pod.MaterialId equals m.MaterialId into materialJoin
                from m in materialJoin.DefaultIfEmpty()
                join s in _context.Suppliers.AsNoTracking().Where(x => x.CompanyId == query.CompanyId)
                    on po.SupplierId equals s.SupplierId into sJoin
                from s in sJoin.DefaultIfEmpty()
                where pod.IsActive
                select new { po, pod, m, s };

            q = q.Where(x => (x.po.OrderType ?? "") == SupplierOrderType);

            q = q.Where(x =>
                EF.Functions.ILike(
                    x.pod.MaterialExternalIDSnapshot ?? "",
                    IncludedMaterialCodePrefix + "%"));

            foreach (var keyworde in ExcludedMaterialCodeKeywords)
            {
                var pattern = "%" + keyworde + "%";

                q = q.Where(x =>
                    !EF.Functions.ILike(
                        x.pod.MaterialExternalIDSnapshot ?? "",
                        pattern));
            }

            q = q.Where(x =>
                string.IsNullOrEmpty(x.po.ExternalId) ||
                !EF.Functions.ILike(x.po.ExternalId, "PO%"));

            if (!query.IncludeInactive)
                q = q.Where(x => x.po.IsActive == true);

            if (fromDate.HasValue)
                q = q.Where(x => x.po.CreateDate >= fromDate.Value);

            if (toDate.HasValue)
                q = q.Where(x => x.po.CreateDate < toDate.Value);

            if (query.CompanyId.HasValue)
                q = q.Where(x => x.po.CompanyId == query.CompanyId.Value);

            if (query.PurchaseOrderId.HasValue)
                q = q.Where(x => x.po.PurchaseOrderId == query.PurchaseOrderId.Value);

            if (query.SupplierId.HasValue)
                q = q.Where(x => x.po.SupplierId == query.SupplierId.Value);

            if (query.MaterialId.HasValue)
                q = q.Where(x => x.pod.MaterialId == query.MaterialId.Value);

            if (!string.IsNullOrWhiteSpace(query.OrderType))
                q = q.Where(x => (x.po.OrderType ?? "") == query.OrderType);

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(x => (x.po.Status ?? "") == query.Status);

            var search = PLPUSmartSearch.Build(q, x => x.po.ExternalId, x => x.s != null ? x.s.ExternalId : "",
                x => x.s != null ? x.s.SupplierName : "", x => x.pod.MaterialExternalIDSnapshot, x => x.pod.MaterialNameSnapshot);
            q = search.ApplyRequest(q, keyword, query, out keyword);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                q = q.Where(x =>
                    (x.po.ExternalId ?? "").Contains(keyword!) ||
                    (x.pod.MaterialExternalIDSnapshot ?? "").Contains(keyword!) ||
                    (x.pod.MaterialNameSnapshot ?? "").Contains(keyword!) ||
                    (x.s != null && (x.s.SupplierName ?? "").Contains(keyword!)) ||
                    (x.s != null && (x.s.ExternalId ?? "").Contains(keyword!))
                );
            }

            return q.Select(x => new PLPUPurchaseOrderLineRaw
            {
                PurchaseOrderId = x.po.PurchaseOrderId,
                POExternalId = x.po.ExternalId ?? string.Empty,
                OrderType = x.po.OrderType ?? string.Empty,

                SupplierId = x.po.SupplierId,
                SupplierName = x.s != null ? (x.s.SupplierName ?? string.Empty) : string.Empty,

                CompanyId = x.po.CompanyId,

                CreatedDate = x.po.CreateDate,
                CreatedBy = x.po.CreatedBy,

                HeaderRequestDeliveryDate = x.po.RequestDeliveryDate,
                RealDeliveryDate = x.po.RealDeliveryDate,

                POStatus = x.po.Status ?? string.Empty,
                PLPUComment = x.po.PLPUComment ?? string.Empty,
                Comment = x.po.Comment ?? string.Empty,
                POIsActive = x.po.IsActive == true,

                PurchaseOrderDetailId = x.pod.PurchaseOrderDetailId,
                LineNo = x.pod.LineNo,

                MaterialId = x.pod.MaterialId,
                MaterialCode = x.pod.MaterialExternalIDSnapshot ?? string.Empty,
                MaterialName = x.pod.MaterialNameSnapshot ?? string.Empty,
                CategoryId = x.m != null ? x.m.CategoryId : null,
                Package = x.pod.Package ?? string.Empty,

                OrderedQuantity = x.pod.RequestQuantity ?? 0,

                BaseCostSnapshot = x.pod.BaseCostSnapshot,
                BaseDateSnapshot = x.pod.BaseDateSnapshot,
                UnitPriceAgreed = x.pod.UnitPriceAgreed,
                TotalPriceAgreed = x.pod.TotalPriceAgreed,

                DeliveryDate = x.pod.DeliveryDate,

                Note = x.pod.Note ?? string.Empty
            });
        }


}
