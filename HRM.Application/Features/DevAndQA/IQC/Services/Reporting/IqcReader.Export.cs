using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Chuẩn bị dữ liệu hai sheet Excel IQC theo bộ lọc, giữ khác biệt keyword và snapshot của từng sheet trong nguồn.</summary>
internal sealed partial class IqcReader
{
    public Task<List<QCInputByQCExportRow>> GetExportRowsAsync(QCInputQuery query, CancellationToken ct) =>
        BuildExportRowsQuery(query).ToListAsync(ct);

    internal IQueryable<QCInputByQCExportRow> BuildExportRowsQuery(QCInputQuery query)
    {
        query ??= new QCInputQuery();

        var hasKeyword = !string.IsNullOrWhiteSpace(query.Keyword);
        var kw = hasKeyword ? query.Keyword!.Trim() : "";

        var requiredHeaderType = (int)WareHouseRequestType.ImportOther;

        var allowedDetailTypes = new[]
        {
            VoucherDetailType.Waiter,

            VoucherDetailType.QCPass,

            VoucherDetailType.QCFail,

            VoucherDetailType.Special

        };

        var q =
            from vd in WarehouseVoucherDetails.AsNoTracking()
            join v in WarehouseVouchers.AsNoTracking()
                on vd.VoucherId equals v.VoucherId

            join movement0 in ReceiptDates
                on vd.VoucherDetailId equals movement0.VoucherDetailId into movementJoin
            from movement in movementJoin.DefaultIfEmpty()
            where v.VoucherType == requiredHeaderType
                  && allowedDetailTypes.Contains(vd.VoucherType)

            join r0 in WarehouseRequests.AsNoTracking()
                on v.RequestId equals r0.RequestId into rJoin
            from r in rJoin.DefaultIfEmpty()

            let qcLatest = QCInputByQCs.AsNoTracking()
                .Where(qc => qc.VoucherDetailId == vd.VoucherDetailId)
                .OrderByDescending(qc => qc.CreatedDate)
                .Select(qc => new
                {
                    qc.QCInputByQCId,
                    qc.CSName,
                    qc.CSExternalId,
                    qc.InspectionMethod,
                    qc.IsCOAProvided,
                    qc.IsMSDSTDSProvided,
                    qc.IsMetalDetectionRequired,
                    qc.IsSuccessQuality,
                    qc.ImportWarehouseType,
                    qc.Note,
                    qc.CreatedDate,
                    qc.CreatedBy
                })
                .FirstOrDefault()

            let qcEmployeeName = qcLatest == null
                ? null
                : Employees.AsNoTracking()
                    .Where(emp => emp.EmployeeId == qcLatest.CreatedBy)
                    .Select(emp => emp.FullName)
                    .FirstOrDefault()

            from po in PurchaseOrders.AsNoTracking()
                .Where(po =>
                    r != null &&
                    po.ExternalId != null &&
                    po.ExternalId == r.codeFromRequest &&
                    po.IsActive == true && (po.Supplier == null || po.Supplier.CompanyId == _companyId)
                )
                .OrderByDescending(po => po.CreateDate)
                .Take(1)
                .DefaultIfEmpty()

            select new
            {
                VoucherDetailId = vd.VoucherDetailId,
                vd.ProductCode,
                vd.ProductName,
                vd.LotNumber,
                vd.QtyKg,
                InputDate = movement == null ? (DateTime?)null : movement.InputDate,

                SupplierName = po != null && po.Supplier != null
                    ? po.Supplier.SupplierName
                    : null,

                qcLatest,
                QCEmployeeName = qcEmployeeName
            };

        if (query.VoucherDetailId > 0)
            q = q.Where(x => x.VoucherDetailId == query.VoucherDetailId);

        if (query.VoucherDetailType.HasValue)
        {
            var t = query.VoucherDetailType.Value;
            if (!allowedDetailTypes.Contains(t))
                return WarehouseVoucherDetails.Where(x => false).Select(x => new QCInputByQCExportRow());

            q =
                from x in q
                join vd in WarehouseVoucherDetails.AsNoTracking()
                    on x.VoucherDetailId equals vd.VoucherDetailId
                where vd.VoucherType == t
                select x;
        }

        if (query.FromDate.HasValue)
            q = q.Where(x => x.InputDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(x => x.InputDate <= query.ToDate.Value);

        if (query.HasQC.HasValue)
        {
            q = query.HasQC.Value
                ? q.Where(x => x.qcLatest != null)
                : q.Where(x => x.qcLatest == null);
        }

        if (query.QCInputByQCId.HasValue)
            q = q.Where(x => x.qcLatest != null && x.qcLatest.QCInputByQCId == query.QCInputByQCId.Value);

        if (hasKeyword)
        {
            var pattern = $"%{kw}%";

            q = q.Where(x =>
                EF.Functions.ILike(x.ProductCode ?? "", pattern) ||
                EF.Functions.ILike(x.ProductName ?? "", pattern) ||
                EF.Functions.ILike(x.LotNumber ?? "", pattern) ||
                EF.Functions.ILike(x.SupplierName ?? "", pattern) ||
                EF.Functions.ILike(x.QCEmployeeName ?? "", pattern) ||
                (x.qcLatest != null && (
                    EF.Functions.ILike(x.qcLatest.CSName ?? "", pattern) ||
                    EF.Functions.ILike(x.qcLatest.CSExternalId ?? "", pattern) ||
                    EF.Functions.ILike(x.qcLatest.Note ?? "", pattern)
                ))
            );
        }

        var rows = q
            .OrderByDescending(x => x.VoucherDetailId)
            .Select(x => new QCInputByQCExportRow
            {
                MaterialExternalId = x.ProductCode,
                MaterialName = x.ProductName,
                LotNo = x.LotNumber,

                ActualReceivedQuantityKg = x.QtyKg,

                SupplierName = x.SupplierName,

                ImportWarehouseText = x.qcLatest == null
                ? ""
                : x.qcLatest.ImportWarehouseType == QcDecision.QCPass ? "Đạt - nhập kho"
                : x.qcLatest.ImportWarehouseType == QcDecision.Special ? "Chấp nhận đặc biệt - nhập kho"
                : x.qcLatest.ImportWarehouseType == QcDecision.QCFail ? "Không đạt - chờ xử lý"
                : "",

                ResultText = x.qcLatest == null
                ? ""
                : x.qcLatest.ImportWarehouseType == QcDecision.Special ? "Chấp nhận đặc biệt"
                : x.qcLatest.ImportWarehouseType == QcDecision.QCPass ? "Đạt"
                : x.qcLatest.ImportWarehouseType == QcDecision.QCFail ? "Không đạt"
                : x.qcLatest.ImportWarehouseType == QcDecision.Waiter ? "Chờ QC"
                : x.qcLatest.IsSuccessQuality == true ? "Đạt"
                : x.qcLatest.IsSuccessQuality == false ? "Không đạt"
                : "",

                InspectorName = x.QCEmployeeName,
                InspectionDate = x.qcLatest != null ? x.qcLatest.CreatedDate : null,
                CreatedDate = x.InputDate,
                Note = x.qcLatest != null ? x.qcLatest.Note : null
            });

        return rows;
    }

    public async Task<List<GetSummaryQCInput>> GetSummaryRowsForExportAsync(QCInputQuery query, CancellationToken ct)
    {
        query ??= new QCInputQuery();

        var idsQuery = BuildSummaryIdsQuery(query);

        var allIds = await idsQuery.ToListAsync(ct);

        if (allIds.Count == 0)
            return new List<GetSummaryQCInput>();

        var itemsQuery = BuildExportSummaryItemsQuery(allIds);

        var items = await itemsQuery
            .OrderByDescending(x => x.VoucherDetailId)
            .ToListAsync(ct);

        return items;
    }

    internal IQueryable<GetSummaryQCInput> BuildExportSummaryItemsQuery(IReadOnlyCollection<long> ids)
    {
        var requiredHeaderType = (int)WareHouseRequestType.ImportOther;
        var allowedDetailTypes = new[] { VoucherDetailType.Waiter, VoucherDetailType.QCPass, VoucherDetailType.QCFail, VoucherDetailType.Special };
        var itemsQuery =
            from vd in WarehouseVoucherDetails.AsNoTracking()
            join v in WarehouseVouchers.AsNoTracking()
                on vd.VoucherId equals v.VoucherId

            join movement0 in ReceiptDates
                on vd.VoucherDetailId equals movement0.VoucherDetailId into movementJoin
            from movement in movementJoin.DefaultIfEmpty()

            join m in Materials.AsNoTracking()
                on vd.ProductCode equals m.ExternalId into mJoin
            from m in mJoin.DefaultIfEmpty()

            join c in Categories.AsNoTracking()
                on m.CategoryId equals c.CategoryId into cJoin
            from c in cJoin.DefaultIfEmpty()

            where v.VoucherType == requiredHeaderType
                  && allowedDetailTypes.Contains(vd.VoucherType)
                  && ids.Contains(vd.VoucherDetailId)

            let qcLatest = QCInputByQCs.AsNoTracking()
                .Where(qc => qc.VoucherDetailId == vd.VoucherDetailId)
                .OrderByDescending(qc => qc.CreatedDate)
                .Select(qc => new
                {
                    qc.QCInputByQCId,
                    qc.CreatedDate,
                    qc.CreatedBy
                })
                .FirstOrDefault()

            join eQc in Employees.AsNoTracking()
                on qcLatest.CreatedBy equals eQc.EmployeeId into eJoin
            from eQc in eJoin.DefaultIfEmpty()

            select new GetSummaryQCInput
            {
                VoucherDetailId = vd.VoucherDetailId,
                ExternalId = vd.ProductCode,
                Name = vd.ProductName,
                LotNumber = vd.LotNumber ?? "",
                QtyKg = vd.QtyKg,
                Unit = "kg",
                VoucherType = vd.VoucherType,
                CategoryName = c != null ? (c.Name ?? "") : "",
                InputDate = movement == null ? (DateTime?)null : movement.InputDate,
                HasQC = qcLatest != null,
                QCInputByQCId = qcLatest != null ? qcLatest.QCInputByQCId : null,
                QCCreatedDate = qcLatest != null ? qcLatest.CreatedDate : null,
                QCEmployeeName = (qcLatest != null && eQc != null) ? (eQc.FullName ?? "") : ""
            };

        return itemsQuery.OrderByDescending(x => x.VoucherDetailId);
    }
}
