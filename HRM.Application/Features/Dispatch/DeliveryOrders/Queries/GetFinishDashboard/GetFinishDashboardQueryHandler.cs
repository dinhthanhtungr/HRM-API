using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Reporting;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetFinishDashboard;

/// <summary>Cùng nguồn Excel Finish theo ngày tạo phiếu; scope KH trước tổng hợp/phân trang. Không trả cost.</summary>
internal sealed class GetFinishDashboardQueryHandler(IDispatchReadDbContext db,
    ICustomerVisibilityReadDbContext customerDb, ICurrentUser user, IDateTimeProvider clock,
    ICurrentUserPermissionService? permissions = null)
    : IRequestHandler<GetFinishDashboardQuery, OperationResult<FinishDashboardDto>>
{
    public async Task<OperationResult<FinishDashboardDto>> Handle(GetFinishDashboardQuery request, CancellationToken ct)
    {
        if (!FinishDashboardScope.CanRead(user, permissions) || user.CompanyId is not { } company || company == Guid.Empty ||
            (!FinishDashboardScope.IsCompany(user) && (user.EmployeeId == null || user.EmployeeId == Guid.Empty)))
            return OperationResult<FinishDashboardDto>.Fail("Không có quyền hoặc thiếu thông tin công ty/nhân viên.");
        if (!Valid(request))
            return OperationResult<FinishDashboardDto>.Fail("Khoảng ngày không hợp lệ; tối đa 366 ngày.");
        var from = request.From.Date;
        var to = request.To.Date;
        var groups = customerDb.MemberInGroups.AsNoTracking()
            .Where(x => x.IsActive && x.IsAdmin == true && x.Profile == user.EmployeeId && x.Group.CompanyId == company)
            .Select(x => x.GroupId);
        var restricted = ImsAccountRestrictions.AppliesTo(user);
        var ids = FinishDashboardScope.Customers(customerDb.Customers.AsNoTracking(), user, clock.Now, groups, request.OwnOnly, permissions).Select(x => x.CustomerId);
        var query = ApplyCustomerScope(DeliveryFinishReportQuery.Project(db.DeliveryOrders.AsNoTracking(), company, from, to, restricted), ids);
        var summary = await Summary(query).SingleOrDefaultAsync(ct) ?? new FinishDashboardSummary();
        if (!restricted)
            summary.RevenueVnd = await Revenue(db.DeliveryOrderDetails.AsNoTracking(), ids, company, from, to.AddDays(1))
                .SumAsync(x => (decimal?)x.RevenueAmountVnd, ct) ?? 0m;
        var days = await query.GroupBy(x => x.CreatedDate.Date).OrderBy(g => g.Key)
            .Select(g => new FinishDashboardDay(g.Key, g.Sum(x => x.Row.QuantityKg), g.Sum(x => (long)x.Row.NumOfBags),
                g.Select(x => x.DeliveryOrderId).Distinct().Count())).ToListAsync(ct);
        var lookup = days.ToDictionary(x => x.Date);
        var daily = Enumerable.Range(0, (to - from).Days + 1)
            .Select(i => { var date = from.AddDays(i); return lookup.GetValueOrDefault(date) ?? new FinishDashboardDay(date, 0, 0, 0); }).ToArray();
        var customers = await query.GroupBy(x => x.CustomerId).OrderByDescending(g => g.Sum(x => x.Row.QuantityKg)).ThenBy(g => g.Key)
            .Select(g => new FinishDashboardRanking(g.Key, g.Max(x => x.Row.CustomerName), g.Sum(x => x.Row.QuantityKg), g.Sum(x => (long)x.Row.NumOfBags)))
            .ToListAsync(ct);
        var products = await query.GroupBy(x => x.ProductId).OrderByDescending(g => g.Sum(x => x.Row.QuantityKg)).ThenBy(g => g.Key)
            .Select(g => new FinishDashboardRanking(g.Key, g.Key.HasValue ? g.Max(x => x.Row.ProductDisplay) : null, g.Sum(x => x.Row.QuantityKg), g.Sum(x => (long)x.Row.NumOfBags)))
            .ToListAsync(ct);
        var page = await query.OrderBy(x => x.CreatedDate).ThenBy(x => x.DeliveryOrderId).ThenBy(x => x.DeliveryLineId)
            .Skip((request.NormalizedPage - 1) * request.NormalizedSize).Take(request.NormalizedSize).ToListAsync(ct);
        await EnrichDates(page, company, ct);
        return OperationResult<FinishDashboardDto>.Ok(new FinishDashboardDto(from, to, restricted || FinishDashboardScope.IsCompany(user) ? "Company" : FinishDashboardScope.IsTeam(user, request.OwnOnly) ? "TeamCustomers" : "OwnCustomers",
            summary, daily, customers, products, new PagedResult<FinishDashboardDetail>(page.Select(x => new FinishDashboardDetail(x.DeliveryLineId, x.Row)).ToArray(), summary.LineCount, request.NormalizedPage, request.NormalizedSize)));
    }

    internal static bool Valid(GetFinishDashboardQuery q) => q.From != default && q.To != default &&
        q.To.Date != DateTime.MaxValue.Date && q.From.Date <= q.To.Date && (q.To.Date - q.From.Date).Days <= 365 &&
        q.NormalizedPage <= int.MaxValue / q.NormalizedSize;

    internal static IQueryable<DeliveryFinishReportSource> ApplyCustomerScope(IQueryable<DeliveryFinishReportSource> rows, IQueryable<Guid> customers)
        => rows.Where(x => customers.Contains(x.CustomerId) && customers.Contains(x.DeliveryCustomerId));

    internal static IQueryable<DeliveryRevenueLine> Revenue(IQueryable<DeliveryOrderDetail> rows,
        IQueryable<Guid> customers, Guid company, DateTime from, DateTime toExclusive)
        => DeliveryRevenueQuery.Create(rows.Where(x => customers.Contains(x.DeliveryOrder.CustomerId)
                && x.MerchandiseOrderDetail != null
                && x.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == company),
            from, toExclusive, company, includeCosts: false, includeDisplayFields: false)
            .Where(x => customers.Contains(x.CustomerId));

    internal static IQueryable<FinishDashboardSummary> Summary(IQueryable<DeliveryFinishReportSource> query) =>
        query.GroupBy(_ => 1).Select(g => new FinishDashboardSummary
        {
            QuantityKg = g.Sum(x => x.Row.QuantityKg), Bags = g.Sum(x => (long)x.Row.NumOfBags),
            DeliveryCount = g.Select(x => x.DeliveryOrderId).Distinct().Count(),
            OrderCount = g.Where(x => x.OrderId.HasValue).Select(x => x.OrderId).Distinct().Count(),
            CustomerCount = g.Select(x => x.CustomerId).Distinct().Count(),
            ProductCount = g.Where(x => x.ProductId.HasValue).Select(x => x.ProductId).Distinct().Count(), LineCount = g.Count()
        });

    private async Task EnrichDates(List<DeliveryFinishReportSource> rows, Guid company, CancellationToken ct)
    {
        var ids = rows.Where(x => x.DetailId.HasValue).Select(x => x.DetailId!.Value).Distinct().ToArray();
        if (ids.Length == 0) return;
        var dates = await db.MfgOrderPOs.AsNoTracking().Where(x => x.IsActive && ids.Contains(x.MerchandiseOrderDetailId) &&
            x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == company && x.Detail.MerchandiseOrder.CompanyId == company)
            .GroupBy(x => x.MerchandiseOrderDetailId).Select(g => new { Id = g.Key, Date = g.Max(x => x.ProductionOrder.ExpectedDate) })
            .ToDictionaryAsync(x => x.Id, x => x.Date, ct);
        foreach (var row in rows) row.Row.ExpectedDeliveryDate = row.DetailId.HasValue ? dates.GetValueOrDefault(row.DetailId.Value) : null;
    }
}
