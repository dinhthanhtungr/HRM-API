using HRM.Application.Abstractions.Persistence.DevAndQA;
using System.Linq.Expressions;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>
/// Đọc danh sách IQC: lọc và phân trang id trước, sau đó lấy context vật tư và QC mới nhất trong company hiện tại.
/// </summary>
internal sealed partial class IqcReader
{
    public async Task<IqcPagedResult<GetSummaryQCInput>> GetPagedSummaryAsync(QCInputQuery query, CancellationToken ct)
    {
        if ((query.SuggestionKeyword is not null || !string.IsNullOrWhiteSpace(query.SearchSelections) ||
             query.Keyword?.IndexOfAny([':', '：']) >= 0) && await ReadSmartSummaryAsync(query, ct) is { } smart)
            return smart;
        if (query.IncludeLabTests)
                return await GetCombinedSummaryAsync(query, ct);
        var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var idsQuery = BuildSummaryIdsQuery(query);

        var total = await idsQuery.CountAsync(ct);

        var pageIds = await idsQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (pageIds.Count == 0)
            return new IqcPagedResult<GetSummaryQCInput>(new List<GetSummaryQCInput>(), total, pageNumber, pageSize);

        var itemsQuery = BuildSummaryItemsQuery(pageIds);

        var items = await itemsQuery
            .OrderByDescending(x => x.VoucherDetailId)
            .ToListAsync(ct);
        foreach (var item in items)
        {
            item.QCDescription = IqcRules.ToVietnamese(item.VoucherType);
        }

        return new IqcPagedResult<GetSummaryQCInput>(items, total, pageNumber, pageSize);
    }

    internal IQueryable<long> BuildSummaryIdsQuery(QCInputQuery query, bool includeKeyword = true)
    {
        var hasKeyword = includeKeyword && !string.IsNullOrWhiteSpace(query.Keyword);
        var kw = hasKeyword ? query.Keyword!.Trim() : "";

        var keywordIncludesQcFields = false;

        var needQcForFilter =
            query.QCInputByQCId.HasValue ||
            query.HasQC.HasValue ||
            (hasKeyword && keywordIncludesQcFields);

        var requiredHeaderType = (int)WareHouseRequestType.ImportOther;

        var allowedDetailTypes = new[]
        {
            VoucherDetailType.Waiter,
            VoucherDetailType.QCPass,
            VoucherDetailType.QCFail,
            VoucherDetailType.Special
        };

        var q1 =
            from vd in WarehouseVoucherDetails.AsNoTracking()
            join v in WarehouseVouchers.AsNoTracking()
                on vd.VoucherId equals v.VoucherId

            where v.VoucherType == requiredHeaderType
               && allowedDetailTypes.Contains(vd.VoucherType)
            select new
            {
                vd,
                v,
                InputDate = (DateTime?)null,
                // Scalar projection được EF bỏ khi không tìm keyword NCC; LEFT LATERAL
                // trước đây vẫn tra DDH cho từng dòng ở mọi nhánh gợi ý mã/tên/lô.
                SupplierName = PurchaseOrders.AsNoTracking()
                    .Where(po => po.ExternalId != null && po.IsActive == true &&
                        (po.Supplier == null || po.Supplier.CompanyId == _companyId) &&
                        WarehouseRequests.Any(header => header.RequestId == v.RequestId &&
                            header.codeFromRequest == po.ExternalId))
                    .OrderByDescending(po => po.CreateDate)
                    .Select(po => po.Supplier != null ? po.Supplier.SupplierName : null)
                    .FirstOrDefault()
            };

        // Ngày nhập chỉ tham gia điều kiện khi người dùng lọc ngày. Không lặp
        // GROUP BY lịch sử kho ở các nhánh gợi ý không cần ngày nhập.
        if (query.FromDate.HasValue || query.ToDate.HasValue)
        {
            q1 = from item in q1
                 join movement0 in ReceiptDates
                     on item.vd.VoucherDetailId equals movement0.VoucherDetailId into movementJoin
                 from movement in movementJoin.DefaultIfEmpty()
                 select new
                 {
                     item.vd,
                     item.v,
                     InputDate = movement == null ? (DateTime?)null : movement.InputDate,
                     item.SupplierName
                 };
        }

        if (query.VoucherDetailId > 0)
            q1 = q1.Where(x => x.vd.VoucherDetailId == query.VoucherDetailId);

        if (query.VoucherDetailType.HasValue)
        {
            var t = query.VoucherDetailType.Value;
            if (!allowedDetailTypes.Contains(t))
                return WarehouseVoucherDetails.Where(x => false).Select(x => x.VoucherDetailId);

            q1 = q1.Where(x => x.vd.VoucherType == t);
        }

        if (query.FromDate.HasValue)
            q1 = q1.Where(x => x.InputDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q1 = q1.Where(x => x.InputDate <= query.ToDate.Value);

        if (hasKeyword)
        {
            var pattern = $"%{kw}%";

            q1 = q1.Where(x =>
                EF.Functions.ILike(x.vd.ProductCode ?? "", pattern) ||
                EF.Functions.ILike(x.vd.ProductName ?? "", pattern) ||
                EF.Functions.ILike(x.vd.LotNumber ?? "", pattern) ||
                EF.Functions.ILike(x.SupplierName ?? "", pattern)
            );
        }

        IQueryable<long> idsQuery;

        if (!needQcForFilter)
        {
            idsQuery = q1
                .OrderByDescending(x => x.vd.VoucherDetailId)
                .Select(x => x.vd.VoucherDetailId);
        }
        else
        {
            var q1WithQc =
                from x in q1
                let qcLatest = QCInputByQCs.AsNoTracking()
                    .Where(qc => qc.VoucherDetailId == x.vd.VoucherDetailId)
                    .OrderByDescending(qc => qc.CreatedDate)
                    .Select(qc => new
                    {
                        qc.QCInputByQCId,
                        qc.CSName,
                        qc.CSExternalId,
                        qc.CreatedDate,
                        qc.CreatedBy
                    })
                    .FirstOrDefault()
                join eQc in Employees.AsNoTracking()
                    on qcLatest.CreatedBy equals eQc.EmployeeId into eJoin
                from eQc in eJoin.DefaultIfEmpty()
                select new
                {
                    x.vd,
                    x.v,
                    qc = qcLatest,
                    QCEmployeeName = eQc != null ? eQc.FullName : null
                };

            if (query.QCInputByQCId.HasValue)
                q1WithQc = q1WithQc.Where(x => x.qc != null && x.qc.QCInputByQCId == query.QCInputByQCId.Value);

            if (query.HasQC.HasValue)
                q1WithQc = query.HasQC.Value
                    ? q1WithQc.Where(x => x.qc != null)
                    : q1WithQc.Where(x => x.qc == null);

            if (hasKeyword && keywordIncludesQcFields)
            {
                q1WithQc = q1WithQc.Where(x =>
                    x.qc != null && (
                        (x.qc.CSName ?? "").Contains(kw) ||
                        (x.qc.CSExternalId ?? "").Contains(kw) ||
                        (x.QCEmployeeName ?? "").Contains(kw)
                    )
                );
            }

            idsQuery = q1WithQc
                .OrderByDescending(x => x.vd.VoucherDetailId)
                .Select(x => x.vd.VoucherDetailId);
        }

        return idsQuery;
    }

    internal IQueryable<GetSummaryQCInput> BuildSummaryItemsQuery(IReadOnlyCollection<long> pageIds)
        => BuildSummaryItemsQueryCore(detail => pageIds.Contains(detail.VoucherDetailId));

    internal IQueryable<GetSummaryQCInput> BuildSummaryItemsQuery(IQueryable<long> pageIds)
        => BuildSummaryItemsQueryCore(detail => pageIds.Contains(detail.VoucherDetailId));

    private IQueryable<GetSummaryQCInput> BuildSummaryItemsQueryCore(Expression<Func<WarehouseVoucherDetail, bool>> predicate)
    {
        var requiredHeaderType = (int)WareHouseRequestType.ImportOther;
        var allowedDetailTypes = new[] { VoucherDetailType.Waiter, VoucherDetailType.QCPass, VoucherDetailType.QCFail, VoucherDetailType.Special };
        var itemsQuery =
            from vd in WarehouseVoucherDetails.AsNoTracking().Where(predicate)
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

            let qcLatest = QCInputByQCs.AsNoTracking()
                .Where(qc => qc.VoucherDetailId == vd.VoucherDetailId)
                .OrderByDescending(qc => qc.CreatedDate)
                .Select(qc => new
                {
                    qc.QCInputByQCId,
                    qc.CreatedDate,
                    qc.CreatedBy,
                    qc.ImportWarehouseType
                })
                .FirstOrDefault()

            join eQc in Employees.AsNoTracking()
                on qcLatest.CreatedBy equals eQc.EmployeeId into eJoin
            from eQc in eJoin.DefaultIfEmpty()

            select new GetSummaryQCInput
            {
                VoucherDetailId = vd.VoucherDetailId,
                ExternalId = m != null ? (m.ExternalId ?? vd.ProductCode ?? "") : (vd.ProductCode ?? ""),
                Name = m != null ? (m.Name ?? vd.ProductName ?? "") : (vd.ProductName ?? ""),
                LotNumber = vd.LotNumber ?? "",
                QtyKg = vd.QtyKg,
                Unit = m != null ? (m.Unit ?? "") : "",
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
