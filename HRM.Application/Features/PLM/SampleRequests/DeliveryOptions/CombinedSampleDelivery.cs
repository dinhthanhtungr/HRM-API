using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

/// <summary>Records a real chip accompanying the kg delivery, without inventing customer feedback.</summary>
internal static class CombinedSampleDelivery
{
    internal static void AddChip(IPLMDbContext db, SampleRequest request, SampleRequestSampleTrial sample,
        int trialNo, Guid actor, DateTime recordedAt, Guid sampleEventId)
    {
        var chip = new SampleRequestSampleTrial
        {
            SampleRequestSampleTrialId = Guid.CreateVersion7(), SampleRequestId = request.SampleRequestId,
            FormulaId = sample.FormulaId, BatchNo = sample.BatchNo, TrialNo = trialNo,
            DeliveredSampleQuantityKg = 0, SentDate = sample.SentDate, SentByEmployeeId = actor,
            Status = SampleTrialStatus.WaitingCustomerFeedback, CustomerReplyStatus = "WAITING",
            LabNote = sample.LabNote, CreatedDate = recordedAt, CreatedBy = actor,
            UpdatedDate = recordedAt, UpdatedBy = actor, IsActive = true
        };
        SampleRequestSampleTrialMutationRules.PopulateSnapshots(chip, request);
        db.SampleRequestSampleTrials.Add(chip);
        var sent = SampleRequestWorkflowEvents.Create(request.SampleRequestId, request.CompanyId, actor, recordedAt,
            SampleRequestWorkflowEvents.SampleSent, sample.SentDate, formulaId: sample.FormulaId, trialId: chip.SampleRequestSampleTrialId);
        // Link to the kg event, not the batch: only kg options participate in batch feedback.
        sent.RelatedEventId = sampleEventId;
        db.SampleRequestWorkflowEvents.Add(sent);
    }
}
