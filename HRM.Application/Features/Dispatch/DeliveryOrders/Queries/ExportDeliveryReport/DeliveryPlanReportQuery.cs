using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;

internal static class DeliveryPlanReportQuery
{
    internal static IQueryable<DeliveryPlanReportSource> Project(IQueryable<MfgOrderPO> links, Guid companyId, DateTime from, DateTime to)
        => links.Where(x => companyId != Guid.Empty && x.IsActive && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive &&
            x.ProductionOrder.CompanyId == companyId && x.Detail.MerchandiseOrder.CompanyId == companyId && x.ProductionOrder.IsActive &&
            x.ProductionOrder.ExpectedDate.HasValue &&
            x.ProductionOrder.ExpectedDate >= from.Date && x.ProductionOrder.ExpectedDate < to.Date.AddDays(1) &&
            x.ProductionOrder.Status != nameof(ManufacturingProductOrder.Canceled) &&
            (x.ProductionOrder.Customer == null || x.ProductionOrder.Customer.ExternalId != InternalCustomerRules.InternalCustomerExternalId))
            .OrderBy(x => x.ProductionOrder.ExpectedDate).ThenBy(x => x.MfgProductionOrderId).ThenBy(x => x.MerchandiseOrderDetailId)
            .Select(x => new DeliveryPlanReportSource
            {
                CustomerName = x.Detail.MerchandiseOrder.CustomerNameSnapshot, Code = x.Detail.ProductExternalIdSnapshot,
                Requested = x.Detail.ExpectedQuantity, Pending = x.ProductionOrder.TotalQuantity == null,
                Allocated = x.Detail.DeliveryOrderDetails.Where(d => d.IsActive && !d.IsAttach && d.DeliveryOrder.IsActive &&
                    d.DeliveryOrder.CompanyId == companyId && d.DeliveryOrder.Status != "Canceled" && d.DeliveryOrder.Status != "Cancelled")
                    .Sum(d => (decimal?)d.Quantity) ?? 0m,
                Lots = x.ProductionOrder.ProductionSelectVersions.Where(v => v.ValidTo == null && v.ManufacturingFormula != null &&
                    v.ManufacturingFormula.CompanyId == companyId).Select(v => v.ManufacturingFormula!.ExternalId).Distinct().ToList(),
                Note = x.Detail.Comment, Address = x.Detail.MerchandiseOrder.DeliveryAddress, ExpectedDate = x.ProductionOrder.ExpectedDate
            });

    public static async Task<List<DeliveryPlanRow>> BuildRowsAsync(IDispatchReadDbContext db, Guid companyId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await Project(db.MfgOrderPOs.AsNoTracking(), companyId, from, to).ToListAsync(ct);
        return rows.Select((row, index) => new DeliveryPlanRow
        {
            Stt = index + 1, CustomerName = row.CustomerName ?? string.Empty, Code = row.Code ?? string.Empty,
            LotNo = string.Join(", ", row.Lots.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
            QuantityText = Math.Max(0m, row.Requested - row.Allocated).ToString(CultureInfo.GetCultureInfo("vi-VN")) + (row.Pending ? "*" : ""),
            QuantityIsPending = row.Pending, Note = row.Note ?? string.Empty, Address = row.Address ?? string.Empty,
            ExpectedDeliveryDate = row.ExpectedDate
        }).ToList();
    }
}

internal sealed class DeliveryPlanReportSource
{
    public string? CustomerName { get; init; }
    public string? Code { get; init; }
    public decimal Requested { get; init; }
    public decimal Allocated { get; init; }
    public bool Pending { get; init; }
    public List<string> Lots { get; init; } = [];
    public string? Note { get; init; }
    public string? Address { get; init; }
    public DateTime? ExpectedDate { get; init; }
}
