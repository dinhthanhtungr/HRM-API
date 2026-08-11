using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

/// <summary>
/// Writes the CRM interaction emitted when a complaint is submitted or resubmitted and links it to the report
/// and source orders. Draft reception edits do not call this writer. Caller owns SaveChanges and the transaction.
/// </summary>
internal sealed class ComplaintInteractionWriter
{
    private readonly IComplaintReportDbContext _dbContext;

    public ComplaintInteractionWriter(IComplaintReportDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        ComplaintReport report,
        string content,
        Guid employeeId,
        DateTime now,
        IReadOnlyCollection<SourceOrderReference> sourceOrders,
        Guid assignedSaleEmployeeId,
        CancellationToken cancellationToken)
    {
        var interactionId = Guid.CreateVersion7();
        await _dbContext.CustomerInteractions.AddAsync(new CustomerInteraction
        {
            Id = interactionId,
            CustomerId = report.CustomerId,
            InteractionType = CustomerInteractionType.Complaint,
            Subject = $"Complaint {report.ExternalId}",
            Content = content.Trim(),
            AssignedSaleEmployeeId = assignedSaleEmployeeId,
            InteractionAt = now,
            CompanyId = report.CompanyId,
            CreatedDate = now,
            CreatedBy = employeeId,
            IsActive = true
        }, cancellationToken);

        await _dbContext.CustomerInteractionReferences.AddAsync(new CustomerInteractionReference
        {
            Id = Guid.CreateVersion7(),
            InteractionId = interactionId,
            ReferenceType = CustomerInteractionReferenceType.ComplaintReport,
            ReferenceId = report.ComplaintReportId,
            ReferenceCodeSnapshot = report.ExternalId,
            ReferenceNameSnapshot = report.Summary,
            IsPrimary = true,
            CompanyId = report.CompanyId,
            CreatedDate = now,
            CreatedBy = employeeId
        }, cancellationToken);

        foreach (var sourceOrder in sourceOrders.DistinctBy(x => x.MerchandiseOrderId))
        {
            await _dbContext.CustomerInteractionReferences.AddAsync(new CustomerInteractionReference
            {
                Id = Guid.CreateVersion7(),
                InteractionId = interactionId,
                ReferenceType = CustomerInteractionReferenceType.MerchandiseOrder,
                ReferenceId = sourceOrder.MerchandiseOrderId,
                ReferenceCodeSnapshot = sourceOrder.ExternalId,
                ReferenceNameSnapshot = sourceOrder.CustomerNameSnapshot,
                IsPrimary = false,
                CompanyId = report.CompanyId,
                CreatedDate = now,
                CreatedBy = employeeId
            }, cancellationToken);
        }
    }

    internal sealed record SourceOrderReference(
        Guid MerchandiseOrderId,
        string ExternalId,
        string CustomerNameSnapshot);
}
