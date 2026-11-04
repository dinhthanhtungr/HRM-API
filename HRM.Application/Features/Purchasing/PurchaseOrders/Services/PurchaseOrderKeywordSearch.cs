using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal static class PurchaseOrderKeywordSearch
{
    internal static IQueryable<PurchaseOrder> Apply(IQueryable<PurchaseOrder> query, string? keyword,
        Guid companyId, bool includeInactive)
        => query.WhereKeyword(keyword, term => po =>
            EF.Functions.ILike(po.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            (po.Supplier != null && po.Supplier.CompanyId == companyId &&
                (EF.Functions.ILike(po.Supplier.SupplierName ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                 EF.Functions.ILike(po.Supplier.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter))) ||
            po.PurchaseOrderDetails.Any(d => (includeInactive || d.IsActive) &&
                (EF.Functions.ILike(d.MaterialExternalIDSnapshot ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                 EF.Functions.ILike(d.MaterialNameSnapshot ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter))) ||
            po.PurchaseOrderLinks.Any(link => (includeInactive || link.IsActive) &&
                link.MerchandiseOrder != null && link.MerchandiseOrder.CompanyId == companyId &&
                EF.Functions.ILike(link.MerchandiseOrder.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter)));
}
