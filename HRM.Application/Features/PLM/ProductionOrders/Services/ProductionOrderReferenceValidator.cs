using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ProductionOrders.Services;

/// <summary>Chặn reference chéo công ty trước khi ghi snapshot, VA hoặc reservation.</summary>
internal sealed class ProductionOrderReferenceValidator(IProductionOrderDbContext db)
{
    public async Task<string?> ValidateInformAsync(
        CreateProductionOrderInformRequest request, Guid companyId, CancellationToken ct)
    {
        var detailProductId = await db.MerchandiseOrderDetails.AsNoTracking()
            .Where(x => x.MerchandiseOrderDetailId == request.MerchandiseOrderDetailId &&
                        x.MerchandiseOrderId == request.MerchandiseOrderId && x.IsActive &&
                        x.MerchandiseOrder.CompanyId == companyId && x.MerchandiseOrder.IsActive)
            .Select(x => (Guid?)x.ProductId).FirstOrDefaultAsync(ct);
        if (!detailProductId.HasValue) return "Không tìm thấy chi tiết đơn hàng.";
        if (detailProductId != request.ProductId) return "ProductId không khớp với chi tiết đơn hàng.";
        if (!await db.Products.AnyAsync(x => x.ProductId == request.ProductId && x.CompanyId == companyId && x.IsActive, ct))
            return "Không tìm thấy sản phẩm trong công ty.";
        if (request.CustomerId.HasValue && !await db.Customers.AnyAsync(
                x => x.CustomerId == request.CustomerId && x.CompanyId == companyId && x.IsActive == true, ct))
            return "Không tìm thấy khách hàng trong công ty.";
        if (request.FormulaCustomerSelect != Guid.Empty && !await db.Formulas.AnyAsync(
                x => x.FormulaId == request.FormulaCustomerSelect && x.CompanyId == companyId && x.IsActive &&
                     x.ProductId == request.ProductId, ct))
            return "Không tìm thấy công thức VU của sản phẩm trong công ty.";

        var items = ProductionOrderCreationRules.ActiveItems(request);
        if (items.Count == 0) return null;
        if (request.ManufacturingFormulaIdIsSelect.HasValue && !await db.ManufacturingFormulas.AnyAsync(
                x => x.ManufacturingFormulaId == request.ManufacturingFormulaIdIsSelect &&
                     x.CompanyId == companyId && x.IsActive, ct))
            return "Không tìm thấy công thức VA nguồn trong công ty.";

        var materialIds = items.Where(x => ProductionOrderCreationRules.IsMaterial(x.ItemType))
            .Select(x => x.ItemId).Distinct().ToArray();
        var productIds = items.Where(x => !ProductionOrderCreationRules.IsMaterial(x.ItemType))
            .Select(x => x.ItemId).Distinct().ToArray();
        var categoryIds = items.Select(x => x.CategoryId).Distinct().ToArray();
        if (await db.Materials.CountAsync(x => materialIds.Contains(x.MaterialId) &&
                x.CompanyId == companyId && x.IsActive == true, ct) != materialIds.Length ||
            await db.Products.CountAsync(x => productIds.Contains(x.ProductId) &&
                x.CompanyId == companyId && x.IsActive, ct) != productIds.Length ||
            await db.Categories.CountAsync(x => categoryIds.Contains(x.CategoryId) &&
                x.CompanyId == companyId && x.IsActive == true, ct) != categoryIds.Length)
            return "Vật tư, sản phẩm hoặc nhóm công thức không hợp lệ trong công ty.";
        return null;
    }
}
