using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;

internal static class DeliveryTransportReportQuery
{
    internal const string UnassignedDeliverer = "Chưa phân người giao";

    internal static IQueryable<DeliveryTransportReportSource> Project(IQueryable<DeliveryOrder> orders, Guid companyId, DateTime from, DateTime to)
        => orders.Where(x => companyId != Guid.Empty && x.IsActive && x.CompanyId == companyId && x.CreatedDate >= from.Date && x.CreatedDate < to.Date.AddDays(1))
            .OrderBy(x => x.CreatedDate).ThenBy(x => x.Id).Select(x => new DeliveryTransportReportSource
            {
                Id = x.Id, ExternalId = x.ExternalId, Date = x.CreatedDate, CustomerCode = x.CustomerExternalIdSnapShot,
                CustomerName = x.Customer != null && x.Customer.CompanyId == companyId ? x.Customer.CustomerName : x.CustomerExternalIdSnapShot,
                CompanyName = x.Company.Name, Address = x.DeliveryAddress, Receiver = x.Receiver, Phone = x.PhoneSnapshot,
                Price = x.DeliveryPrice ?? 0m, Note = x.Note,
                Deliverers = x.Deliverers.Where(d => d.DelivererInfor.IsActive).Select(d => d.DelivererInfor.Name).ToList(),
                Lines = x.Details.Where(d => d.IsActive).Select(d => new DeliveryTransportLineSource
                {
                    Code = d.ProductExternalIdSnapShot, Name = d.ProductNameSnapShot, Quantity = d.Quantity, Bags = d.NumOfBags,
                    Lot = d.LotConsumptions.Any(l => l.IsActive) ? string.Join(", ", d.LotConsumptions.Where(l => l.IsActive)
                        .OrderBy(l => l.LotNo).Select(l => l.LotNo)) : d.LotNoList, Po = d.PONo
                }).ToList()
            });

    public static async Task<DeliveryTransportWorkbookData> BuildTransportWorkbookDataAsync(IDispatchReadDbContext db, Guid companyId,
        DateTime from, DateTime to, CancellationToken ct)
        => Build(await Project(db.DeliveryOrders.AsNoTracking(), companyId, from, to).ToListAsync(ct));

    internal static DeliveryTransportWorkbookData Build(IEnumerable<DeliveryTransportReportSource> orders)
    {
        var details = new List<DeliveryTransportReportRow>();
        var companyName = string.Empty;
        foreach (var order in orders)
        {
            companyName = order.CompanyName;
            if (order.Lines.Count == 0) continue;
            var names = order.Deliverers.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0) names.Add(UnassignedDeliverer);
            foreach (var name in names)
                details.Add(new DeliveryTransportReportRow
                {
                    DeliveryOrderId = order.Id, DeliveryExternalId = order.ExternalId, DeliveryDate = order.Date,
                    DelivererName = name, CustomerCode = order.CustomerCode, CustomerName = order.CustomerName,
                    Address = order.Address, Receiver = order.Receiver, Phone = order.Phone,
                    ProductDisplay = string.Join("; ", order.Lines.Select(x => $"{x.Code?.Trim()} - {x.Name?.Trim()}".Trim(' ', '-')).Where(x => x.Length > 0).Distinct()),
                    LotNoDisplay = Join(order.Lines.Select(x => x.Lot)), PoNoDisplay = Join(order.Lines.Select(x => x.Po)),
                    TotalQuantity = order.Lines.Sum(x => x.Quantity), TotalBags = order.Lines.Sum(x => x.Bags),
                    DeliveryPrice = order.Price / names.Count, Note = order.Note
                });
        }
        var grouped = details.GroupBy(x => x.DelivererName.Trim(), StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DeliveryDate).ThenBy(x => x.DeliveryExternalId).ToList(), StringComparer.OrdinalIgnoreCase);
        return new DeliveryTransportWorkbookData
        {
            CompanyName = companyName, DetailByDeliverer = grouped,
            SummaryRows = grouped.Select((g, index) => new DeliveryTransportSummaryRow
            {
                Stt = index + 1, DelivererName = g.Key, TotalTrips = g.Value.Count,
                TotalQuantity = g.Value.Sum(x => x.TotalQuantity), TotalAmount = g.Value.Sum(x => x.DeliveryPrice), AdvanceAmount = 0m
            }).ToList()
        };
    }
    private static string Join(IEnumerable<string?> values) => string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
}

internal sealed class DeliveryTransportReportSource
{
    public Guid Id { get; init; }
    public string? ExternalId { get; init; }
    public DateTime? Date { get; init; }
    public string? CustomerCode { get; init; }
    public string? CustomerName { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Receiver { get; init; }
    public string? Phone { get; init; }
    public decimal Price { get; init; }
    public string? Note { get; init; }
    public List<string> Deliverers { get; init; } = [];
    public List<DeliveryTransportLineSource> Lines { get; init; } = [];
}

internal sealed class DeliveryTransportLineSource
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Lot { get; init; }
    public string? Po { get; init; }
    public decimal Quantity { get; init; }
    public int Bags { get; init; }
}
