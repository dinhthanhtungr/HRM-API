using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockMovements;

internal sealed class GetStockMovementsQueryHandler(IWarehouseReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<GetStockMovementsQuery, OperationResult<PagedResult<StockMovementDto>>>
{
    public async Task<OperationResult<PagedResult<StockMovementDto>>> Handle(GetStockMovementsQuery request, CancellationToken ct)
    {
        using var timing = new WarehouseMovementQueryTiming();
        if (!StockMovementReader.CanRead(user))
            return OperationResult<PagedResult<StockMovementDto>>.Fail("Bạn không có quyền xem biến động kho.");
        if (request.FromDate > request.ToDate || request.Keyword?.Length > 200 || request.PageNumber > 1000000 ||
            request.Scope is not (null or "" or "NORMAL" or "CT") ||
            (!string.IsNullOrEmpty(request.BusinessType) && !StockMovementReader.BusinessTypes.Contains(request.BusinessType)))
            return OperationResult<PagedResult<StockMovementDto>>.Fail("Bộ lọc không hợp lệ.");

        var companyRows = db.ReadMovementHistory(user.CompanyId!.Value).Where(x => x.CompanyId == user.CompanyId);
        var canViewMixing = permissions.HasPermission(ApplicationPermissions.Warehouse.ViewMixingStock);
        var selectedGroups = StockMovementListQuery.Groups(companyRows, canViewMixing);
        // Complete-group MIN(CreatedAt) equals MIN(MovementAt), without reading the view's window column.
        if (request.FromDate.HasValue) selectedGroups = selectedGroups.Where(g => g.Min(x => x.CreatedAt) >= request.FromDate.Value);
        if (request.ToDate.HasValue) selectedGroups = selectedGroups.Where(g => g.Min(x => x.CreatedAt) <= request.ToDate.Value);
        if (!string.IsNullOrEmpty(request.Scope)) selectedGroups = selectedGroups.Where(g => g.Any(x => x.Scope == request.Scope));
        // Suggestion value lookups retain their existing independently authorized source.
        // This source is not executed by the regular list/count/page path.
        var search = StockMovementSmartSearch.Build(StockMovementReader.VisibleRows(db, user, permissions));
        selectedGroups = search.ApplyRequest(selectedGroups, request.NormalizedKeyword, request, out var keyword);
        selectedGroups = selectedGroups.WhereKeyword(keyword, term => g => g.Any(x =>
            EF.Functions.ILike(x.ProductCode!, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.LotNumber!, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.ShelfStockCode, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.RequestCode!, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter)));
        // Classify the complete group before applying row-level search/scope, matching the summary.
        if (!string.IsNullOrEmpty(request.BusinessType))
        {
            var rank = Array.IndexOf(StockMovementReader.BusinessTypes, request.BusinessType);
            var mixingRequests = StockMovementRequestClassifier.MixingExportCodes(db, user.CompanyId!.Value);
            selectedGroups = selectedGroups.Where(g => g.Min(x => x.MovementType == "QC_WAIT_TO_RAW_PASS" ? 10 : x.AppSource == "Warehouse.InventoryCheck" ? 0
                : x.MovementType == "NORMAL_TO_CT" ? 1 : x.MovementType == "CT_TO_NORMAL" ? 2
                : x.MovementType == "NORMAL_INTERNAL" || x.MovementType == "CT_INTERNAL" || x.MovementType == "INTERNAL_MIXED" || x.MovementType == "VERIFIED_LEGACY_TRANSFER" ? 3
                : x.AppSource == "Warehouse.AdjustUI" ? 4
                : x.MovementType == "NORMAL_OUT" && x.Scope == "NORMAL" && x.DeltaKg < 0
                    && x.RequestCode != null && mixingRequests.Contains(x.RequestCode) ? 5
                : x.VoucherId.HasValue ? 6 : !x.IsCanonicalMovement ? 9 : x.DeltaKg > 0 ? 7 : 8) == rank);
        }

        timing.RecordScope();
        if (await timing.RunAsync("suggestions", () => search.SuggestionsAsync<StockMovementDto>(selectedGroups, request, ct)) is {} suggestions)
            return OperationResult<PagedResult<StockMovementDto>>.Ok(suggestions);
        var groups = selectedGroups
            .Select(g => new { Key = g.Key, At = g.Min(x => x.CreatedAt), LastId = g.Max(x => x.LedgerId) });
        var count = await timing.RunAsync("count", () => groups.CountAsync(ct));
        var keys = await timing.RunAsync("page", () => groups.OrderByDescending(x => x.At).ThenByDescending(x => x.LastId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize).Select(x => x.Key).ToArrayAsync(ct));
        // Fetch complete groups after paging, even when only the destination matched the search.
        var rows = await timing.RunAsync("rows", () => StockMovementListQuery.PageRows(companyRows, keys, canViewMixing).ToListAsync(ct));
        var context = await timing.RunAsync("context", () => StockMovementContextReader.ReadAsync(db, user.CompanyId!.Value, rows, ct, timing));
        var items = timing.Run("mapping", () =>
        {
            var byKey = rows.GroupBy(x => x.MovementKey).ToDictionary(x => x.Key, x => StockMovementReader.Summarize(x.ToArray(), context));
            return keys.Where(byKey.ContainsKey).Select(key => byKey[key]).ToArray();
        });
        return OperationResult<PagedResult<StockMovementDto>>.Ok(new PagedResult<StockMovementDto>(items, count, request.NormalizedPageNumber, request.NormalizedPageSize));
    }
}
