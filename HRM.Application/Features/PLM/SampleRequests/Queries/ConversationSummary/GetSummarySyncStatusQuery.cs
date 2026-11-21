using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using MediatR;
namespace HRM.Application.Features.PLM.SampleRequests.Queries.ConversationSummary;
public sealed record GetSummarySyncStatusQuery : IRequest<SummarySyncStatus?>;
