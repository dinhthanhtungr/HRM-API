using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using MediatR;
using System.ComponentModel.DataAnnotations;
namespace HRM.Application.Features.PLM.SampleRequests.Commands.SyncConversationSummaries;
public sealed record SyncConversationSummariesCommand(Guid? Cursor = null, [Range(1, 100)] int Take = 100, Guid? SampleRequestId = null)
    : IRequest<SummaryBackfillPage?>;
