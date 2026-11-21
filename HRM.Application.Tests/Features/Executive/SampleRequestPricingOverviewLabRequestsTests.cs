using System.Text.Json;
using HRM.Application.Features.Executive.SampleRequestPricingOverview;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using LabRequest = HRM.Application.Features.Executive.SampleRequestPricingOverview.GetSampleRequestPricingOverviewQueryHandler.LabPriceQuoteRequestRow;

namespace HRM.Application.Tests.Features.Executive;

public sealed class SampleRequestPricingOverviewLabRequestsTests
{
    private static readonly DateTime RequestedAt = new(2026, 10, 20, 10, 0, 0);

    [Fact]
    public void WorkflowConfirmationRequiresPricingBeforeDeliveryAndUsesBusinessTimeAfterApproval()
    {
        var sample = Sample();
        var companyId = Guid.NewGuid();
        var evt = new SampleRequestWorkflowEvent
        {
            CompanyId = companyId, SampleRequestId = sample.SampleRequestId,
            EventType = "LabFormulaConfirmed", FormulaId = Guid.NewGuid(), RecordedAt = RequestedAt
        };
        var requests = GetSampleRequestPricingOverviewQueryHandler.SelectLabPriceQuoteRequests(
            Array.Empty<InternalConversation>().AsQueryable(), companyId, new[] { evt }.AsQueryable());

        Assert.Single(Pending([sample], requests, []));
        // Transport can finish later; approval is compared with the confirmation, not delivery time.
        evt.MessageId = Guid.NewGuid();
        Assert.Empty(Pending([sample], requests, [Approval(sample.ProductId, RequestedAt.AddMinutes(1))]));
        evt.CompanyId = Guid.NewGuid();
        Assert.Empty(requests);
    }

    [Fact]
    public void WorkflowPricingRequestQueryTranslatesAndExcludesDeliveredMessageDuplicate()
    {
        using var db = CreateDbContext();
        var sql = GetSampleRequestPricingOverviewQueryHandler.SelectLabPriceQuoteRequests(
            db.InternalConversations, Guid.NewGuid(), db.SampleRequestWorkflowEvents).ToQueryString();
        Assert.Contains("LabFormulaConfirmed", sql, StringComparison.Ordinal);
        Assert.Contains("RecordedAt", sql, StringComparison.Ordinal);
        Assert.Contains("SentAt", sql, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("UNION ALL", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void LabRequest_WaitsForInitialOrNewerApproval(int? approvalOffsetMinutes, bool expected)
    {
        var sample = Sample();
        var approvals = approvalOffsetMinutes.HasValue
            ? new[] { Approval(sample.ProductId, RequestedAt.AddMinutes(approvalOffsetMinutes.Value)) }
            : Array.Empty<ProductPricingVersion>();

        Assert.Equal(expected, Pending([sample], [Request(sample)], approvals).Any());
    }

    [Fact]
    public void LabRequest_FirstApprovalResolvesRequestAndNewLabRequestReopensIt()
    {
        var sample = Sample();
        var requests = new List<LabRequest> { Request(sample) };
        var approvals = new List<ProductPricingVersion>();

        Assert.Single(Pending([sample], requests, approvals));
        approvals.Add(Approval(sample.ProductId, RequestedAt.AddMinutes(1)));
        Assert.Empty(Pending([sample], requests, approvals));

        requests.Add(new LabRequest
        {
            SampleRequestId = sample.SampleRequestId,
            RequestedAt = RequestedAt.AddMinutes(2)
        });
        Assert.Single(Pending([sample], requests, approvals));
        approvals[0].ApprovedAt = RequestedAt.AddMinutes(3);
        Assert.Empty(Pending([sample], requests, approvals));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LabRequest_UsesLegacyApprovalDateFallback(bool hasUpdatedDate)
    {
        var sample = Sample();
        var approval = Approval(sample.ProductId, RequestedAt);
        approval.ApprovedAt = null;
        approval.CreatedDate = RequestedAt.AddMinutes(hasUpdatedDate ? -1 : 0);
        approval.UpdatedDate = hasUpdatedDate ? RequestedAt : null;

        Assert.Empty(Pending([sample], [Request(sample)], [approval]));
    }

    [Fact]
    public void LabRequest_DoesNotIncludeOtherSamplesOrDuplicateCardsForMultipleMessages()
    {
        var sample = Sample();
        var otherSample = new SampleRequest { SampleRequestId = Guid.NewGuid(), ProductId = sample.ProductId };
        var requests = new[] { Request(sample), Request(sample) };

        Assert.Equal(sample.SampleRequestId,
            Assert.Single(Pending([sample, otherSample], requests, [])).SampleRequestId);
        Assert.Empty(Pending([otherSample], requests, []));
    }

    [Fact]
    public void LabRequest_ApprovalOfAnotherProductDoesNotResolveIt()
    {
        var sample = Sample();
        Assert.Single(Pending([sample], [Request(sample)],
            [Approval(Guid.NewGuid(), RequestedAt.AddMinutes(1))]));
        Assert.Empty(Pending([sample], [], []));
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void LabRequest_IgnoresOutOfScopeOrInactiveConversations(
        bool sameCompany, bool active, bool sampleRelatedType, bool hasRelatedId)
    {
        var companyId = Guid.NewGuid();
        var conversation = new InternalConversation
        {
            CompanyId = sameCompany ? companyId : Guid.NewGuid(), IsActive = active,
            RelatedType = sampleRelatedType ? InternalMailRelatedType.SampleRequest : InternalMailRelatedType.Quotation,
            RelatedId = hasRelatedId ? Guid.NewGuid() : null,
            Messages = [new InternalMessage { PayloadJson = "{}", SentAt = RequestedAt }]
        };

        Assert.Empty(GetSampleRequestPricingOverviewQueryHandler.SelectLabPriceQuoteRequests(
            new[] { conversation }.AsQueryable(), companyId));
    }

    [Theory]
    [InlineData(true, "{}")]
    [InlineData(false, null)]
    public void LabRequest_IgnoresDeletedMessagesOrMissingPayload(bool deleted, string? payload)
    {
        var companyId = Guid.NewGuid();
        var conversation = new InternalConversation
        {
            CompanyId = companyId, RelatedType = InternalMailRelatedType.SampleRequest,
            RelatedId = Guid.NewGuid(),
            Messages = [new InternalMessage { IsDeleted = deleted, PayloadJson = payload, SentAt = RequestedAt }]
        };
        Assert.Empty(GetSampleRequestPricingOverviewQueryHandler.SelectLabPriceQuoteRequests(
            new[] { conversation }.AsQueryable(), companyId));
    }

    [Fact]
    public void LabRequest_UsesPersistedPayloadMarkerAndTranslatesToPostgresBeforePagination()
    {
        using var db = CreateDbContext();
        var companyId = Guid.NewGuid();
        var requests = GetSampleRequestPricingOverviewQueryHandler.SelectLabPriceQuoteRequests(
            db.InternalConversations.AsNoTracking(), companyId);
        var approvals = db.ProductPricingVersions.Where(x =>
            x.CompanyId == companyId && x.IsActive && x.Currency == "VND" &&
            x.Status == ProductPricingStatus.Approved);
        var sql = GetSampleRequestPricingOverviewQueryHandler.ApplyPendingLabPriceQuoteRequests(
                db.SampleRequests.Where(x => x.CompanyId == companyId && x.IsActive), requests, approvals)
            .Union(db.SampleRequests.Where(x => x.CompanyId == companyId && !x.IsActive))
            .OrderBy(x => x.CreatedDate).Skip(12).Take(12).ToQueryString();

        Assert.Contains("SampleRequestPriceQuoteRequested", sql, StringComparison.Ordinal);
        Assert.Contains("LabConfirmed", sql, StringComparison.Ordinal);
        Assert.Contains("Explicit", sql, StringComparison.Ordinal);
        Assert.Contains("@>", sql, StringComparison.Ordinal);
        Assert.Contains("IsDeleted", sql, StringComparison.Ordinal);
        Assert.Contains("RelatedType", sql, StringComparison.Ordinal);
        Assert.Contains("RelatedId", sql, StringComparison.Ordinal);
        Assert.Contains("CompanyId", sql, StringComparison.Ordinal);
        Assert.Contains("IsActive", sql, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("ApprovedAt", sql, StringComparison.Ordinal);
        Assert.Contains("SentAt", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.Contains("UNION", sql, StringComparison.Ordinal);

        // Same serializer and DTO used by SendSampleRequestMessageCommandHandler.
        var json = JsonSerializer.Serialize(new SampleRequestThreadMessagePayload
        {
            ContentType = SampleRequestPriceQuotePayloadTypes.Request,
            PriceQuoteRequest = new SampleRequestPriceQuotePayload { FormulaSelectionSource = "LabConfirmed" }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var payload = JsonDocument.Parse(json);
        Assert.Equal("SampleRequestPriceQuoteRequested", payload.RootElement.GetProperty("contentType").GetString());
        Assert.Equal("LabConfirmed", payload.RootElement.GetProperty("priceQuoteRequest")
            .GetProperty("formulaSelectionSource").GetString());
    }

    private static IQueryable<SampleRequest> Pending(
        IEnumerable<SampleRequest> samples,
        IEnumerable<LabRequest> requests,
        IEnumerable<ProductPricingVersion> approvals)
        => GetSampleRequestPricingOverviewQueryHandler.ApplyPendingLabPriceQuoteRequests(
            samples.AsQueryable(), requests.AsQueryable(), approvals.AsQueryable());

    private static SampleRequest Sample() => new()
    {
        SampleRequestId = Guid.NewGuid(), ProductId = Guid.NewGuid()
    };

    private static LabRequest Request(SampleRequest sample) => new()
    {
        SampleRequestId = sample.SampleRequestId, RequestedAt = RequestedAt
    };

    private static ProductPricingVersion Approval(Guid productId, DateTime approvedAt) => new()
    {
        ProductId = productId, Status = ProductPricingStatus.Approved, ApprovedAt = approvedAt
    };

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test").Options);
}
