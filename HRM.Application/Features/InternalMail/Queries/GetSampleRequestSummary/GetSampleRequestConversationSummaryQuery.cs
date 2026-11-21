using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using MediatR;
namespace HRM.Application.Features.InternalMail.Queries.GetSampleRequestSummary;
public sealed record GetSampleRequestConversationSummaryQuery(Guid ConversationId) : IRequest<ConversationSummaryDto?>;
