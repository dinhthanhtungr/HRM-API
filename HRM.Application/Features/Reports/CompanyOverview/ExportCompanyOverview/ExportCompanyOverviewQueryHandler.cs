using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportOrders;
using HRM.Application.Features.Reports.Sales.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.CompanyOverview.ExportCompanyOverview;

internal sealed class ExportCompanyOverviewQueryHandler(ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, ISender sender,
    IReportReadDbContext db, PLPUPurchaseOverviewReportService rawMaterials,
    ICompanyOverviewReader purchases, ICompanyOverviewExcelRenderer renderer)
    : IRequestHandler<ExportCompanyOverviewQuery, OperationResult<CompanyOverviewExportFile>>
{
    public async Task<OperationResult<CompanyOverviewExportFile>> Handle(ExportCompanyOverviewQuery request, CancellationToken ct)
    {
        if (!CompanyOverviewAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<CompanyOverviewExportFile>.Fail("Access denied for current company or capability.");
        var period = SalesReportPeriod.Create(request.From, request.To, clock.Now.Date);
        if (period is null)
            return OperationResult<CompanyOverviewExportFile>.Fail("Invalid date range (maximum 1096 days).");
        var companyId = user.CompanyId!.Value;
        var report = await sender.Send(new GetCompanyOverviewQuery
        { CompanyId = companyId, From = period.From, To = period.To }, ct);
        if (!report.Success || report.Data is null)
            return OperationResult<CompanyOverviewExportFile>.Fail(report.Message ?? "Report is unavailable.");

        // All readers share the scoped DbContext; keep these operations sequential.
        var sales = await new SalesReportDrawerReader(db, user).DeliveriesAsync(new GetSalesReportOrdersQuery
        { CompanyId = companyId, From = period.From, To = period.To, Metric = "deliveredOrder" }, period, ct,
            includeDisplayFields: true);
        var nvl = await rawMaterials.GetExportLinesAsync(new PLPUPurchaseOverviewQuery
        { CompanyId = companyId, From = period.From, To = period.To }, ct);
        var facts = await purchases.ReadPurchasesAsync(companyId, period.From, period.ToExclusive, ct);
        var supplies = facts.Where(x => x.Kind == "Supplies" && x.Source == "PurchaseOrder")
            .OrderBy(x => x.Date).ThenBy(x => x.DocumentCode).ThenBy(x => x.ItemCode).ToArray();
        var orderIds = sales.Select(x => x.MerchandiseOrderId).Distinct().ToArray();
        var orderCodes = await db.MerchandiseOrders.AsNoTracking()
            .Where(x => x.CompanyId == companyId && orderIds.Contains(x.MerchandiseOrderId))
            .Select(x => new { x.MerchandiseOrderId, x.ExternalId })
            .ToDictionaryAsync(x => x.MerchandiseOrderId, x => x.ExternalId, ct);
        var rows = sales.OrderBy(x => x.RevenueDate).ThenBy(x => x.MerchandiseOrderId)
            .ThenBy(x => x.ProductKey).Select(x => new CompanySalesExportRow(x.RevenueDate,
                orderCodes.GetValueOrDefault(x.MerchandiseOrderId) ?? string.Empty,
                x.CustomerName, x.ProductKey, x.ProductName, x.Quantity, x.RevenueAmountVnd)).ToArray();
        const int maxRows = 1_000_000;
        if (rows.Length > maxRows || nvl.Count > maxRows || supplies.Length > maxRows)
            return OperationResult<CompanyOverviewExportFile>.Fail("Export is too large. Select a shorter period.");
        ct.ThrowIfCancellationRequested();
        return OperationResult<CompanyOverviewExportFile>.Ok(new CompanyOverviewExportFile(
            renderer.Render(new(report.Data, rows, nvl, supplies)),
            $"Tong-hop-cong-ty_{period.From:yyyyMMdd}_{period.To:yyyyMMdd}.xlsx"));
    }
}
