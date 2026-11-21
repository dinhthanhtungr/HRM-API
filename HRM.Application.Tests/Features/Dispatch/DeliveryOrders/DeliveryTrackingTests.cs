using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryTrackingTests
{
    [Fact]
    public void RestrictedFinishReportDoesNotSelectLotPoOrNoteColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var day = new DateTime(2026, 11, 1);
        var sql = DeliveryFinishReportQuery.Project(
            db.DeliveryOrders, Guid.NewGuid(), day, day, restricted: true).ToQueryString();
        Assert.DoesNotContain("\"LotNoList\"", sql);
        Assert.DoesNotContain("\"PONo\"", sql);
        Assert.DoesNotContain("\"Note\"", sql);
        Assert.DoesNotContain("\"DeliveryPrice\"", sql);
    }

    [Fact]
    public void ProjectionsTranslateWithoutCostsLotsOrFormula()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var delivery = db.DeliveryOrders.Where(order => order.Id == Guid.Empty);
        var header = DeliveryTrackingReader.Project(delivery).ToQueryString();
        var lines = DeliveryTrackingReader.ProjectLines(delivery).ToQueryString();

        Assert.Contains("CustomerName", header);
        Assert.Contains("ProductNameSnapShot", lines);
        Assert.Contains("Quantity", lines);
        Assert.DoesNotContain("DeliveryPrice", header);
        Assert.DoesNotContain("LotNoList", lines);
        Assert.DoesNotContain("FormulaId", lines);
    }
}
