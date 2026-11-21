using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.ExportStockAvailable;

public sealed record ExportStockAvailableQuery(GetStockAvailableQuery Filters) : IRequest<OperationResult<byte[]>>;

internal sealed class ExportStockAvailableQueryHandler(ISender sender, ICurrentUser user, IStockAvailableExcelRenderer renderer)
    : IRequestHandler<ExportStockAvailableQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(ExportStockAvailableQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not Guid company || company == Guid.Empty)
            return OperationResult<byte[]>.Fail("Không có quyền xuất tồn kho.");
        var filter = request.Filters;
        var result = await sender.Send(new GetStockAvailableQuery
        {
            ExportAll = true, StockTypes = filter.StockTypes, Keyword = filter.Keyword,
            SearchSelections = filter.SearchSelections,
            OnlyAvailableLeZero = filter.OnlyAvailableLeZero, AvailableMax = filter.AvailableMax,
            SortBy = filter.SortBy, SortDirection = filter.SortDirection
        }, ct);
        if (!result.Success || result.Data == null) return OperationResult<byte[]>.Fail(result.Message ?? "Không xuất được tồn kho.");
        if (result.Data.Items.Sum(x => (long)x.StockDetailAvailables.Count) > 100000)
            return OperationResult<byte[]>.Fail("Tối đa 100.000 dòng chi tiết mỗi lần xuất. Vui lòng thu hẹp bộ lọc.");
        var visibility = new StockAvailableExportVisibility(
            user.Roles.Any(role => ApplicationRoleSets.Warehouse.InactiveShelfFieldViewers.Contains(role, StringComparer.OrdinalIgnoreCase)));
        return OperationResult<byte[]>.Ok(renderer.Render(result.Data.Items, visibility, ct));
    }
}
