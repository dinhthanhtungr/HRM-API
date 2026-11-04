using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>Checks every product against company/customer history without writing any data.</summary>
internal sealed class SaleOrderStandardSampleService(ISaleOrderDbContext db, ICustomerVisibilityService visibility)
{
    public async Task<OperationResult<IReadOnlyList<StandardSampleReviewDto>>> ReviewAsync(
        CreateSaleOrderRequest request, Guid companyId, DateTime now, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.SaleOrderDetails is null)
            return OperationResult<IReadOnlyList<StandardSampleReviewDto>>.Fail("Khách hàng hoặc danh sách sản phẩm không hợp lệ.");
        var scope = await visibility.BuildScopeAsync(ct);
        var customerAllowed = await visibility.ApplyCustomerVisibility(db.Customers.AsNoTracking(), scope)
            .AnyAsync(x => x.CustomerId == request.CustomerId && x.CompanyId == companyId && x.IsActive == true, ct)
            || await db.Customers.AsNoTracking().AnyAsync(x => x.CustomerId == request.CustomerId
                && x.CompanyId == companyId && x.IsActive == true && x.ExternalId == PLMCustomerRules.InternalCustomerExternalId, ct);
        if (!customerAllowed)
            return OperationResult<IReadOnlyList<StandardSampleReviewDto>>.Fail("Không có quyền tạo đơn cho khách hàng này.");

        var lines = request.SaleOrderDetails.Where(x => x.ProductId != Guid.Empty && x.FormulaId != Guid.Empty
            && x.ExpectedQuantity > 0).DistinctBy(x => (x.ProductId, x.FormulaId)).ToArray();
        if (lines.Length == 0)
            return OperationResult<IReadOnlyList<StandardSampleReviewDto>>.Fail("Đơn hàng phải có ít nhất một dòng hợp lệ.");
        var ids = lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.ProductId)
            && x.CompanyId == companyId && x.IsActive)
            .Select(x => new { x.ProductId, x.ColourCode, x.Code, x.Name }).ToDictionaryAsync(x => x.ProductId, ct);
        if (products.Count != ids.Length)
            return OperationResult<IReadOnlyList<StandardSampleReviewDto>>.Fail("Sản phẩm không hợp lệ trong công ty hiện tại.");

        var history = await db.MerchandiseOrders.AsNoTracking().Where(x => x.CompanyId == companyId
                && x.CustomerId == request.CustomerId && x.IsActive
                && x.MerchandiseOrderId != request.MerchandiseOrderId
                && (x.Status == nameof(MerchadiseStatus.Approved) || x.Status == nameof(MerchadiseStatus.Processing)
                    || x.Status == nameof(MerchadiseStatus.Delivering) || x.Status == nameof(MerchadiseStatus.Delivered)
                    || x.Status == nameof(MerchadiseStatus.Completed)))
            .SelectMany(x => x.MerchandiseOrderDetails.Where(d => d.IsActive && ids.Contains(d.ProductId)
                && d.Status != nameof(MerchadiseStatus.Cancelled)
                && d.Status != nameof(MerchadiseStatus.WaitingForStandardSample)), (order, detail) => new { detail.ProductId, order.CreateDate })
            .GroupBy(x => x.ProductId).Select(g => new { ProductId = g.Key, LastOrderDate = g.Max(x => x.CreateDate) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastOrderDate, ct);
        IReadOnlyList<StandardSampleReviewDto> result = lines
            .Where(x => !history.TryGetValue(x.ProductId, out var date) || date.Date < now.Date.AddYears(-2))
            .Select(x => new StandardSampleReviewDto(x.ProductId, x.FormulaId,
                products[x.ProductId].ColourCode ?? products[x.ProductId].Code ?? string.Empty,
                products[x.ProductId].Name ?? string.Empty,
                history.TryGetValue(x.ProductId, out var date) ? date : null)).ToArray();
        return OperationResult<IReadOnlyList<StandardSampleReviewDto>>.Ok(result);
    }
}
