using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Services;

internal sealed class DeliveryAttachedGoodsService(IDispatchWriteDbContext db)
{
    public async Task<OperationResult<List<DeliveryOrderDetail>>> BuildAsync(
        IReadOnlyCollection<DeliveryAttachedGoodsRequest>? rows, Guid companyId, CancellationToken ct)
    {
        if (rows is null || rows.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0m || x.NumOfBags < 0 ||
            x.LotNoList?.Length > 1000 || x.PONo?.Length > 100))
            return OperationResult<List<DeliveryOrderDetail>>.Fail("Thông tin hàng kèm không hợp lệ.");
        var ids = rows.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.ProductId) && x.CompanyId == companyId && x.IsActive)
            .Select(x => new { x.ProductId, x.ColourCode, x.Name }).ToListAsync(ct);
        if (products.Count != ids.Length) return OperationResult<List<DeliveryOrderDetail>>.Fail("Có sản phẩm hàng kèm không thuộc công ty hoặc đã khóa.");
        return OperationResult<List<DeliveryOrderDetail>>.Ok(rows.Select(row =>
        {
            var product = products.Single(x => x.ProductId == row.ProductId);
            return new DeliveryOrderDetail
            {
                Id = Guid.CreateVersion7(), ProductId = row.ProductId, ProductExternalIdSnapShot = product.ColourCode,
                ProductNameSnapShot = product.Name, Quantity = row.Quantity, NumOfBags = row.NumOfBags,
                LotNoList = row.LotNoList?.Trim(), PONo = row.PONo?.Trim(), IsActive = true, IsAttach = true
            };
        }).ToList());
    }
}
