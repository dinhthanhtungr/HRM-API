using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockMovementByKey;

internal sealed class GetStockMovementByKeyQueryHandler(IWarehouseReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<GetStockMovementByKeyQuery, OperationResult<StockMovementDetailDto>>
{
    public async Task<OperationResult<StockMovementDetailDto>> Handle(GetStockMovementByKeyQuery request, CancellationToken ct)
    {
        if (!StockMovementReader.CanRead(user) || string.IsNullOrWhiteSpace(request.MovementKey) || request.MovementKey.Length > 100)
            return OperationResult<StockMovementDetailDto>.Fail("Không tìm thấy biến động kho.");
        var visible = StockMovementReader.VisibleRows(db, user, permissions);
        var key = await StockMovementReader.ResolveLegacyKeyAsync(visible, request.MovementKey, ct);
        var rows = await visible.Where(x => x.MovementKey == key).OrderBy(x => x.LedgerId).ToListAsync(ct);
        if (rows.Count == 0) return OperationResult<StockMovementDetailDto>.Fail("Không tìm thấy biến động kho.");
        var context = await StockMovementContextReader.ReadAsync(db, user.CompanyId!.Value, rows, ct);
        var lines = rows.Select(x => new StockMovementLineDto(x.LedgerId, x.CreatedAt, x.ShelfStockCode, x.Scope,
            x.ProductCode, x.LotNumber, x.StockType, x.UnitName, x.BeforeKg, x.DeltaKg, x.AfterKg, x.CreatedBy,
            x.CreatedBy.HasValue ? context.Employees.GetValueOrDefault(x.CreatedBy.Value) : null, x.RequestCode, x.AppSource,
            x.VoucherId, x.VoucherId.HasValue ? context.Vouchers.GetValueOrDefault(x.VoucherId.Value) : null)).ToArray();
        return OperationResult<StockMovementDetailDto>.Ok(new StockMovementDetailDto(StockMovementReader.Summarize(rows, context), lines));
    }
}
