using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Services.Requests;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestTrackingTests
{
    [Fact]
    public void ScopeAndProjectionExcludePrivateRecordsAndCoarsenSensitiveStatus()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var visible = Row(company, "public", "CUSTOMER");
        store.Seed(nameof(db.SampleRequests),
            visible,
            Row(company, "Nội bộ", "CUSTOMER"),
            Row(company, "private", "CUSTOMER"),
            Row(company, "public", " kh_vietaus "),
            Row(Guid.NewGuid(), "public", "CUSTOMER"));

        var record = Assert.Single(SampleRequestTrackingReader.Project(
            SampleRequestTrackingReader.VisibleRequests(db, company)));

        Assert.Equal(visible.SampleRequestId, record.SampleRequestId);
        Assert.Equal("InProgress", record.Status);
        Assert.Equal("TP-001", record.ProductCode);
    }

    [Fact]
    public void TrackingProjectionTranslatesToPostgresWithoutReadingFormulaOrPriceColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = SampleRequestTrackingReader.Project(
            SampleRequestTrackingReader.VisibleRequests(db, Guid.NewGuid()).Take(25)).ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("\"FormulaId\"", sql);
        Assert.DoesNotContain("\"ExpectedPrice\"", sql);
        Assert.DoesNotContain("\"AttachmentCollectionId\"", sql);
    }

    [Fact]
    public void MilestonesExcludePrivateForeignPricingAndOptionEvents()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var visible = Row(company, "public", "CUSTOMER");
        var hidden = Row(company, "private", "CUSTOMER");
        var date = new DateTime(2026, 8, 1);
        SampleRequestWorkflowEvent Event(Guid requestId, Guid eventCompany, string type, int day, Guid? related = null)
            => new() { SampleRequestId = requestId, CompanyId = eventCompany, EventType = type,
                RecordedAt = date.AddDays(day), RelatedEventId = related, Note = "hidden note", FormulaId = Guid.NewGuid() };
        store.Seed(nameof(db.SampleRequests), visible, hidden);
        store.Seed(nameof(db.SampleRequestWorkflowEvents),
            Event(visible.SampleRequestId, company, SampleRequestWorkflowEvents.Created, 0),
            Event(visible.SampleRequestId, company, SampleRequestWorkflowEvents.SampleSent, 1),
            Event(visible.SampleRequestId, company, SampleDeliveryRules.BatchSent, 2),
            Event(visible.SampleRequestId, company, SampleRequestWorkflowEvents.SampleSent, 3, Guid.NewGuid()),
            Event(visible.SampleRequestId, company, SampleRequestWorkflowEvents.PriceAccepted, 4),
            Event(hidden.SampleRequestId, company, SampleRequestWorkflowEvents.RequestCancelled, 4),
            Event(visible.SampleRequestId, Guid.NewGuid(), SampleRequestWorkflowEvents.RequestCancelled, 4));
        var milestones = SampleRequestTrackingReader.ProjectMilestones(db,
            SampleRequestTrackingReader.VisibleRequests(db, company), company).ToArray();
        Assert.Collection(milestones,
            row => { Assert.Equal("SampleSent", row.Status); Assert.Equal(date.AddDays(2), row.At); },
            row => { Assert.Equal("New", row.Status); Assert.Equal(date, row.At); });
    }

    [Fact]
    public void MilestoneProjectionTranslatesWithoutEventPayloadColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var sql = SampleRequestTrackingReader.ProjectMilestones(db,
            SampleRequestTrackingReader.VisibleRequests(db, company), company).ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.DoesNotContain("FormulaId", sql);
        Assert.DoesNotContain("PriceReference", sql);
        Assert.DoesNotContain("Note", sql);
        Assert.DoesNotContain("ConversationId", sql);
    }

    private static SampleRequest Row(Guid company, string requestType, string customerCode) => new()
    {
        SampleRequestId = Guid.NewGuid(),
        ExternalId = "SR-001",
        CompanyId = company,
        IsActive = true,
        RequestType = requestType,
        Status = "WaitingCustomerPriceConfirmation",
        Product = new Product { CompanyId = company, IsActive = true, Code = "TP-001" },
        Customer = new Customer { CompanyId = company, IsActive = true, ExternalId = customerCode },
    };
}
