using System.Security.Cryptography;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordQuotationFeedback;

/// <summary>One CRM interaction with shared feedback and optional pricing notes; ordinary CRM visibility applies.</summary>
internal static class QuotationFeedbackInteraction
{
    public static Guid? Id(Guid company, Guid actor, RecordSampleQuotationFeedbackCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Feedback?.CustomerFeedbackNote)) return null;
        // Stable for an exact retry, including a lost HTTP response. No new schema/key required.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Source = "SampleQuotationFeedback/shared-v2", Company = company,
            Actor = actor, Request = command.SampleRequestId, command.ExpectedUpdatedDate, command.QuotedAt, command.Feedback });
        return new Guid(SHA256.HashData(bytes).AsSpan(0, 16));
    }

    public static Task<bool> ExistsAsync(ICRMWriteDbContext db, Guid id, SampleRequest request, Guid actor, CancellationToken ct)
        => db.CustomerInteractions.AsNoTracking().AnyAsync(x => x.Id == id && x.CompanyId == request.CompanyId &&
            x.CustomerId == request.CustomerId && x.CreatedBy == actor && x.InteractionType == CustomerInteractionType.Quotation &&
            x.References.Any(r => r.CompanyId == request.CompanyId && r.ReferenceType == CustomerInteractionReferenceType.SampleRequest &&
                r.ReferenceId == request.SampleRequestId), ct);

    public static void Add(ICRMWriteDbContext db, Guid id, SampleRequest request, Guid actor,
        SampleQuotationFeedbackInput feedback, Customer customer, DateTime now)
    {
        var occurredAt = feedback.OccurredAt ?? now;
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            Id = id, CompanyId = request.CompanyId, CustomerId = request.CustomerId,
            AssignedSaleEmployeeId = request.ManagerBy, CreatedBy = actor, CreatedDate = now,
            InteractionAt = occurredAt, InteractionType = CustomerInteractionType.Quotation,
            Subject = "Phản hồi báo giá · " + request.ExternalId,
            Content = Content(feedback), Outcome = feedback.Outcome, IsActive = true
        });
        db.CustomerInteractionReferences.Add(new CustomerInteractionReference
        {
            Id = Guid.CreateVersion7(), CompanyId = request.CompanyId, InteractionId = id,
            ReferenceType = CustomerInteractionReferenceType.SampleRequest, ReferenceId = request.SampleRequestId,
            ReferenceCodeSnapshot = request.ExternalId, IsPrimary = true, CreatedBy = actor, CreatedDate = now
        });
        if (!customer.LastContactDate.HasValue || occurredAt > customer.LastContactDate)
            customer.LastContactDate = occurredAt;
        // Recording on behalf of Sales must not transfer customer ownership or create a follow-up task.
    }

    internal static string Content(SampleQuotationFeedbackInput feedback)
        => "Nội dung gửi Lab:\n" + feedback.CustomerFeedbackNote!.Trim() +
            (string.IsNullOrWhiteSpace(feedback.Note) ? string.Empty : "\n\nGhi chú trao đổi giá:\n" + feedback.Note.Trim());
}
