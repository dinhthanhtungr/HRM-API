using HRM.Application.Features.CRM.Quotations.Queries.Tracking;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationTrackingTests
{
    [Theory]
    [InlineData(QuotationStatus.PendingApproval, "InProgress")]
    [InlineData(QuotationStatus.Approved, "InProgress")]
    [InlineData(QuotationStatus.PendingSendConfirmation, "InProgress")]
    [InlineData(QuotationStatus.SendRejected, "InProgress")]
    [InlineData(QuotationStatus.Sent, "Sent")]
    [InlineData(QuotationStatus.Accepted, "Accepted")]
    public void TrackingUsesOperationalStatus(QuotationStatus sourceStatus, string expectedStatus)
    {
        var quotation = new Quotation
        {
            ExternalId = "BG-001",
            Customer = new Customer { CustomerName = "Customer" },
            Status = sourceStatus,
        };

        var result = Assert.Single(QuotationTrackingReader.Project(new[] { quotation }.AsQueryable()));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("BG-001", result.ExternalId);
    }
}
