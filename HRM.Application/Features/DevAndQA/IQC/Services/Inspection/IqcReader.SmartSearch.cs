using System.Linq.Expressions;
using System.Diagnostics;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

internal sealed partial class IqcReader
{
    internal sealed class SmartRow
    {
        public long VoucherDetailId { get; init; }
        public Guid? LabTestId { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string LotNumber { get; init; } = "";
        public DateTime SortDate { get; init; }
    }

    internal SmartSearchSchema<SmartRow> BuildSmartSchema(IQueryable<SmartRow> rows)
    {
        Expression<Func<SmartRow, IEnumerable<PurchaseOrder>>> orders = row => PurchaseOrders.Where(po => po.Supplier != null &&
            po.Supplier.CompanyId == _companyId && WarehouseVoucherDetails.Any(detail => detail.VoucherDetailId == row.VoucherDetailId &&
                WarehouseRequests.Any(header => header.RequestId == detail.Voucher.RequestId && header.codeFromRequest == po.ExternalId)));
        return SmartSearch.For(rows, fields => [
            fields.Code("materialCode", x => x.LabTestId == null ? x.Code : "", "Mã NVL", "Material code"),
            fields.Name("materialName", x => x.LabTestId == null ? x.Name : "", "Tên NVL", "Material name"),
            fields.Code("inspectionCode", x => x.LabTestId != null ? x.Code : "", "Mã kiểm tra", "Inspection code"),
            fields.Name("testName", x => x.LabTestId != null ? x.Name : "", "Tên mẫu", "Test name"),
            fields.Code("batchNumber", x => x.LotNumber, "Số lô", "Batch"),
            fields.Codes("supplierCode", orders, x => x.Supplier!.ExternalId, "Mã NCC", "Supplier code") with {
                Values = scoped => BuildSmartSupplierOrders(scoped).Select(x => x.Supplier!.ExternalId ?? "")
            },
            fields.Names("supplierName", orders, x => x.Supplier!.SupplierName, "Tên NCC", "Supplier name") with {
                Values = scoped => BuildSmartSupplierOrders(scoped).Select(x => x.Supplier!.SupplierName ?? "")
            }
        ]);
    }

    // Gợi ý NCC đi theo tập phiếu hợp lệ, không quét lại mọi DDH cho từng dòng kho.
    // Giữ biểu thức badge/prefix hiện có và mọi bộ lọc do service Smart Search truyền vào scoped.
    internal IQueryable<PurchaseOrder> BuildSmartSupplierOrders(IQueryable<SmartRow> scoped)
    {
        var detailIds = scoped.Where(x => x.LabTestId == null).Select(x => x.VoucherDetailId);
        return from detail in WarehouseVoucherDetails.AsNoTracking()
               join header in WarehouseRequests.AsNoTracking()
                   on detail.Voucher.RequestId equals (int?)header.RequestId
               join order in PurchaseOrders.AsNoTracking()
                   on header.codeFromRequest equals order.ExternalId
               where detailIds.Contains(detail.VoucherDetailId) &&
                   order.Supplier != null && order.Supplier.CompanyId == _companyId
               select order;
    }

    private async Task<IqcPagedResult<GetSummaryQCInput>?> ReadSmartSummaryAsync(QCInputQuery request, CancellationToken ct)
    {
        var rows = BuildSmartRows(request);
        var schema = BuildSmartSchema(rows);
        if (request.SuggestionKeyword is null && string.IsNullOrWhiteSpace(request.SearchSelections) && !schema.IsScopedKeyword(request.Keyword))
            return null;
        rows = schema.ApplyRequest(rows, request.Keyword, request, out var keyword);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var legacyIds = BuildSummaryIdsQuery(request);
            var labs = context.LabTests.WhereKeyword(keyword, term => x =>
                EF.Functions.ILike(x.Code, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.SampleName, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.BatchNumber ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter)).Select(x => x.Id);
            rows = rows.Where(x => x.LabTestId == null ? legacyIds.Contains(x.VoucherDetailId) : labs.Contains(x.LabTestId.Value));
        }
        if (request.SuggestionKeyword is not null)
        {
            var activity = Activity.Current;
            var started = activity?.GetTagItem(IqcSuggestionDiagnosticTags.Enabled) is true
                ? Stopwatch.GetTimestamp() : (long?)null;
            try
            {
                var suggestions = await schema.SuggestionsAsync<GetSummaryQCInput>(rows, request, ct);
                return new([], 0, 1, 10) { SearchSuggestions = suggestions!.SearchSuggestions };
            }
            finally
            {
                if (started.HasValue)
                    activity!.SetTag(IqcSuggestionDiagnosticTags.SuggestionMilliseconds,
                        (long)Stopwatch.GetElapsedTime(started.Value).TotalMilliseconds);
            }
        }
        var total = await rows.CountAsync(ct);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(100, request.PageSize);
        var ordered = request.IncludeLabTests ? rows.OrderByDescending(x => x.SortDate).ThenByDescending(x => x.VoucherDetailId).ThenByDescending(x => x.LabTestId)
            : rows.OrderByDescending(x => x.VoucherDetailId);
        var page = await ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new SummarySourceKey { VoucherDetailId = x.VoucherDetailId, LabTestId = x.LabTestId, SortDate = x.SortDate }).ToListAsync(ct);
        return await LoadSummaryPageAsync(page, total, pageNumber, pageSize, ct);
    }

    internal IQueryable<SmartRow> BuildSmartRows(QCInputQuery request)
    {
        var ids = BuildSummaryIdsQuery(request, includeKeyword: false);
        // Gợi ý chỉ cần mã/tên/lô; giữ bộ lọc ID, không dựng lại QC mới nhất, category,
        // người QC và ledger dành cho dòng chi tiết ở mỗi nhánh UNION.
        var warehouse = request.SuggestionKeyword is not null
            ? BuildSmartWarehouseMetadata(ids)
            : BuildSummaryItemsQuery(ids).Select(x => new SmartRow {
                VoucherDetailId = x.VoucherDetailId, LabTestId = null, Code = x.ExternalId, Name = x.Name, LotNumber = x.LotNumber,
                SortDate = x.InputDate ?? WarehouseVoucherDetails.Where(d => d.VoucherDetailId == x.VoucherDetailId)
                    .Select(d => d.Voucher.CreatedDate).FirstOrDefault()
            });
        if (!request.IncludeLabTests) return warehouse;
        var labs = context.LabTests.AsNoTracking().Where(x => x.CompanyId == _companyId);
        if (request.VoucherDetailId > 0 || request.QCInputByQCId.HasValue || request.VoucherDetailType.HasValue || request.HasQC == false)
            labs = labs.Where(_ => false);
        if (request.FromDate.HasValue) { var from = DateOnly.FromDateTime(request.FromDate.Value); labs = labs.Where(x => x.TestDate >= from); }
        if (request.ToDate.HasValue) { var to = DateOnly.FromDateTime(request.ToDate.Value); labs = labs.Where(x => x.TestDate <= to); }
        return warehouse.Concat(labs.Select(x => new SmartRow { VoucherDetailId = 0, LabTestId = x.Id, Code = x.Code,
            Name = x.SampleName, LotNumber = x.BatchNumber ?? "", SortDate = x.CreatedDate }));
    }

    private IQueryable<SmartRow> BuildSmartWarehouseMetadata(IQueryable<long> ids)
        => from detail in WarehouseVoucherDetails.AsNoTracking()
            where ids.Contains(detail.VoucherDetailId)
            join material in Materials.AsNoTracking() on detail.ProductCode equals material.ExternalId into materials
            from material in materials.DefaultIfEmpty()
            select new SmartRow {
                VoucherDetailId = detail.VoucherDetailId,
                LabTestId = null,
                Code = material != null ? (material.ExternalId ?? detail.ProductCode ?? "") : (detail.ProductCode ?? ""),
                Name = material != null ? (material.Name ?? detail.ProductName ?? "") : (detail.ProductName ?? ""),
                LotNumber = detail.LotNumber ?? "",
                SortDate = default
            };
}
