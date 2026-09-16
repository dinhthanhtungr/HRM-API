using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Notifications.Commands.BackfillSampleRequestPriceQuoteRecipients;

/// <summary>
/// Bổ sung inbox notification Báo giá thiếu cho participant active của Sample Request trong khoảng thời gian hẹp.
/// Mặc định chỉ preview; backfill không tạo outbox hay phát lại realtime/Web Push.
/// </summary>
public sealed record BackfillSampleRequestPriceQuoteRecipientsCommand(
    DateTime? From = null,
    DateTime? To = null,
    bool DryRun = true)
    : IRequest<OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>>;

public sealed record BackfillSampleRequestPriceQuoteRecipientsResult(
    DateTime From,
    DateTime To,
    bool DryRun,
    int NotificationCount,
    int ConversationCount,
    int MissingConversationReferenceCount,
    int CandidateParticipantCount,
    int RecipientRecordsCreated,
    int UserStatesCreated,
    int MutedUserStatesCreated);
