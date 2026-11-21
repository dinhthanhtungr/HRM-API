using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequest;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class SampleRequestFollowUpCreator
{
    /// <summary>A VU already sent in kg is never reopened for editing. Start a new TP without copying its VU.</summary>
    internal static async Task<OperationResult<Guid>> CreateAsync(IPLMWriteDbContext db, ISender sender,
        SampleRequest source, ApplySampleFlowActionCommand command, Guid actor, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var locked = await db.SampleRequests.Where(x => x.SampleRequestId == source.SampleRequestId &&
            x.CompanyId == source.CompanyId && x.UpdatedDate == command.ExpectedUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
        if (locked != 1) return OperationResult<Guid>.Fail("Request changed. Reload before saving.");
        var now = DateTime.Now;
        source.UpdatedDate = now;
        source.UpdatedBy = actor;
        var created = await sender.Send(Build(source, command), ct);
        if (!created.Success) return created;
        db.SampleRequestWorkflowEvents.Add(new SampleRequestWorkflowEvent
        {
            Id = Guid.CreateVersion7(), CompanyId = source.CompanyId, SampleRequestId = source.SampleRequestId,
            EventType = "FollowUpRequestCreated", RecordedAt = now, OccurredAt = now, RecordedByEmployeeId = actor,
            Note = $"Follow-up SampleRequest: {created.Data}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    internal static CreateSampleRequestCommand Build(SampleRequest source, ApplySampleFlowActionCommand command) => new()
    {
        UseExistingTransaction = true, CustomerId = source.CustomerId, ProductId = source.ProductId,
        ChipBeforeSample = command.Action == ChipThenSampleRules.Action,
        CompanyId = source.CompanyId, BranchId = source.BranchId, RequestType = source.RequestType,
        InfoType = command.Action is "RequestChip" or ChipThenSampleRules.Action ? $"Thẻ màu ({command.RequestedSets})" : source.InfoType,
        SampleQuantity = command.Action is "RequestSample" or ChipThenSampleRules.Action ? command.RequestedKg : 0,
        InitialLabMessage = $"Yêu cầu tiếp theo từ {source.ExternalId}. " +
            (command.Action == ChipThenSampleRules.Action ? $"Gửi chip {command.RequestedSets} bộ trước, chip đạt rồi chạy mẫu {command.RequestedKg} kg. " :
                command.Action == "RequestSample" ? $"Yêu cầu chạy mẫu {command.RequestedKg} kg. " : $"Yêu cầu gửi chip {command.RequestedSets} bộ. ") + command.Note
    };
}
