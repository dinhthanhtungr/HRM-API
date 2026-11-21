using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestPriceAuditTests
{
    [Fact]
    public void AcceptanceKeepsActualCustomerDateSeparateFromRecordedDateAndNotes()
    {
        var recorded = new DateTime(2026, 10, 31, 14, 30, 0);
        var accepted = recorded.AddDays(-1);
        var employee = Guid.NewGuid();
        var line = Guid.NewGuid();
        var audit = SampleRequestCustomerPriceAudit.CreateEvent(Guid.NewGuid(), Guid.NewGuid(), employee,
            recorded, false, "WaitingCustomerPriceConfirmation", "InProgress", accepted, line, "BG-1 v2", "Delivery Friday");
        var dto = SampleRequestCustomerPriceAudit.Read(audit, "Sale A");
        Assert.True(dto.IsActive);
        Assert.Equal(recorded, dto.RecordedAt);
        Assert.Equal(accepted, dto.CustomerAcceptedAt);
        Assert.Equal(employee, dto.RecordedByEmployeeId);
        Assert.Equal(line, dto.QuotationLineId);
        Assert.Equal("Delivery Friday", dto.Note);
        Assert.Equal("BG-1 v2", dto.PriceReference);
        Assert.Equal(audit.Id, dto.WorkflowEventId);
    }

    [Fact]
    public void RevocationDoesNotLookLikeActiveAcceptanceAndPreservesReason()
    {
        var audit = SampleRequestCustomerPriceAudit.CreateEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Now, true, "InProgress", "WaitingCustomerPriceConfirmation", null, null, "BG-1", "Customer changed conditions");
        var dto = SampleRequestCustomerPriceAudit.Read(audit);
        Assert.False(dto.IsActive);
        Assert.Null(dto.CustomerAcceptedAt);
        Assert.Null(dto.QuotationLineId);
        Assert.Equal("Customer changed conditions", dto.Note);
    }

    [Fact]
    public void PrototypeAuditCanBeReadWithoutMappingPrototypeColumns()
    {
        var audit = new AuditLog { Reason = "CustomerPriceConfirmed", ChangedAt = DateTime.Now,
            NewValues = JsonSerializer.SerializeToDocument(new { CustomerPriceConfirmedAt = DateTime.Now,
                CustomerPriceReference = "Legacy quotation", CustomerPriceConfirmationNote = "Legacy note" }) };
        var dto = SampleRequestCustomerPriceAudit.Read(audit);
        Assert.True(dto.IsActive);
        Assert.Equal("Legacy quotation", dto.PriceReference);
        Assert.Equal("Legacy note", dto.Note);
    }

    [Fact]
    public void RevocationLinksToExactAcceptanceWithoutLosingActualCustomerDate()
    {
        var accepted = SampleRequestWorkflowEvents.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 11, 1), SampleRequestWorkflowEvents.PriceAccepted, new DateTime(2026, 10, 30));
        var revoked = SampleRequestWorkflowEvents.Create(accepted.SampleRequestId, accepted.CompanyId, Guid.NewGuid(),
            new DateTime(2026, 11, 2), SampleRequestWorkflowEvents.PriceRevoked, note: "Sale ghi nhầm giá");
        revoked.RelatedEventId = accepted.Id;
        var dto = SampleRequestCustomerPriceAudit.Read(revoked, "Sale", accepted);
        Assert.False(dto.IsActive);
        Assert.Equal(accepted.OccurredAt, dto.CustomerAcceptedAt);
        Assert.Equal("Sale ghi nhầm giá", dto.Note);
    }

    [Fact]
    public void GenericPatchAndProductAutoStartCannotBypassWaitingPrice()
    {
        Assert.NotNull(SampleRequestCustomerPriceRules.ValidateStatusPatch("WaitingCustomerPriceConfirmation", "InProgress"));
        Assert.NotNull(SampleRequestCustomerPriceRules.ValidateStatusPatch("InProgress", "WaitingCustomerPriceConfirmation"));
        Assert.Null(SampleRequestCustomerPriceRules.ValidateStatusPatch("WaitingCustomerPriceConfirmation", "Cancelled"));
        Assert.Null(SampleRequestCustomerPriceRules.ValidateStatusPatch("WaitingCustomerPriceConfirmation", "SampleSent"));
        var record = new SampleRequest { Status = "WaitingCustomerPriceConfirmation" };
        Assert.False(SampleRequestStatusTransitionRules.TryStartProcessing(record, new Product { Name = "Product", ColourCode = "C1" }));
        Assert.Equal("WaitingCustomerPriceConfirmation", record.Status);
        Assert.Equal("InProgress", SampleRequestCustomerPriceRules.ResolveSubmittedStatus("InProgress", "Thẻ màu (2)"));
        Assert.Equal("InProgress", SampleRequestCustomerPriceRules.ResolveSubmittedStatus("InProgress", "Quotation"));
    }

    [Theory]
    [InlineData("Thẻ màu (1)")]
    [InlineData("Thẻ màu (2)")]
    [InlineData("ColorSampleSet")]
    [InlineData("ColourSampleSet")]
    [InlineData("Quotation")]
    [InlineData("Báo giá")]
    public void SubmittedRequestsRequireBothProductNameAndColourCode(string infoType)
    {
        Product?[] incompleteProducts =
        [
            null,
            new Product(),
            new Product { Name = "Product" },
            new Product { ColourCode = "C1" },
            new Product { Name = " ", ColourCode = "C1" },
            new Product { Name = "Product", ColourCode = " " }
        ];
        foreach (var product in incompleteProducts)
        {
            var initialStatus = SampleRequestStatusTransitionRules.ResolveInitialStatus(product);
            Assert.Equal("New", SampleRequestCustomerPriceRules.ResolveSubmittedStatus(initialStatus, infoType));
            var request = new SampleRequest { Status = "New" };
            Assert.False(SampleRequestStatusTransitionRules.TryStartProcessing(request, product));
            Assert.Equal("New", request.Status);
        }

        var identifiedProduct = new Product { Name = "Product", ColourCode = "C1" };
        var readyStatus = SampleRequestStatusTransitionRules.ResolveInitialStatus(identifiedProduct);
        Assert.Equal("InProgress", SampleRequestCustomerPriceRules.ResolveSubmittedStatus(readyStatus, infoType));
        var readyRequest = new SampleRequest { Status = "New" };
        Assert.True(SampleRequestStatusTransitionRules.TryStartProcessing(readyRequest, identifiedProduct));
        Assert.Equal("InProgress", readyRequest.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PriceEventsAfterDeliveryNeverRewindSampleSent(bool revoke)
    {
        Assert.Equal("SampleSent", SampleRequestCustomerPriceRules.ResolveStatusAfterPriceEvent("SampleSent", revoke));
        Assert.Equal("InProgress", SampleRequestCustomerPriceRules.ResolveStatusAfterPriceEvent("InProgress", revoke));
    }
}
