using HRM.Application.Abstractions.Persistence.PLM;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialRequirements.Queries.GetMaterialRequirementDemands;

internal static class MaterialRequirementSaleOrderQueries
{
    public static IQueryable<MaterialRequirementSaleOrderRow> ActiveLinks(IPLMReadDbContext db, Guid companyId)
        => from link in db.MfgOrderPOs.AsNoTracking()
           join detail in db.MerchandiseOrderDetails.AsNoTracking() on link.MerchandiseOrderDetailId equals detail.MerchandiseOrderDetailId
           join sale in db.MerchandiseOrders.AsNoTracking() on detail.MerchandiseOrderId equals sale.MerchandiseOrderId
           join production in db.MfgProductionOrders.AsNoTracking() on link.MfgProductionOrderId equals production.MfgProductionOrderId
           join customer in db.Customers.AsNoTracking() on sale.CustomerId equals customer.CustomerId
           where link.IsActive && detail.IsActive && sale.IsActive && production.IsActive &&
                 sale.CompanyId == companyId && production.CompanyId == companyId &&
                 customer.CompanyId == companyId && customer.IsActive == true
           select new MaterialRequirementSaleOrderRow
           {
               ProductionOrderId = link.MfgProductionOrderId,
               SaleOrderId = sale.MerchandiseOrderId,
               SaleOrderCode = sale.ExternalId,
               SaleOrderDetailId = detail.MerchandiseOrderDetailId,
               CustomerId = customer.CustomerId,
               CustomerCode = customer.ExternalId,
               CustomerName = customer.CustomerName
           };
}

internal sealed class MaterialRequirementSaleOrderRow
{
    public Guid ProductionOrderId { get; init; }
    public Guid SaleOrderId { get; init; }
    public string SaleOrderCode { get; init; } = string.Empty;
    public Guid SaleOrderDetailId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
}
