using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.DirectPatchNotifications;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDirectPatchNotification;

internal sealed class CreateSampleRequestDirectPatchNotificationCommandHandler
    : IRequestHandler<CreateSampleRequestDirectPatchNotificationCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxMessageLength = 2000;
    private const int MaxChanges = 50;
    private const int MaxLabelLength = 200;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ISender _sender;

    public CreateSampleRequestDirectPatchNotificationCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _sender = sender;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        CreateSampleRequestDirectPatchNotificationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("SampleRequestId is invalid.");
        }

        if (request.IdempotencyKey == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("IdempotencyKey is required.");
        }

        var employeeId = _currentUser.EmployeeId;
        var companyId = _currentUser.CompanyId;
        if (!employeeId.HasValue || employeeId.Value == Guid.Empty ||
            !companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Current user is invalid.");
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                $"Message is required and cannot exceed {MaxMessageLength} characters.");
        }

        var changes = NormalizeAndValidateChanges(request.Changes, out var changeError);
        if (changeError is not null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(changeError);
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var sampleRequest = await _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests
                    .Where(x => x.SampleRequestId == request.SampleRequestId)
                    .AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .Select(x => new
            {
                x.SampleRequestId,
                x.ExternalId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Sample request was not found.");
        }

        var existingResult = await FindExistingResultAsync(
            request.SampleRequestId,
            companyId.Value,
            request.IdempotencyKey,
            cancellationToken);
        if (existingResult is not null)
        {
            return OperationResult<SendInternalMessageResultDto>.Ok(
                existingResult,
                "Direct patch notification was already sent.");
        }

        return await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = message,
            IsUrgent = request.IsUrgent,
            ExtraRecipientEmployeeIds = request.RecipientEmployeeIds,
            TopicOverride = TopicNotifications.SampleRequestDirectPatchNotified,
            TitleOverride = "Sale đã điều chỉnh yêu cầu phối mẫu",
            DirectPatchNotification = new SampleRequestDirectPatchNotificationPayload
            {
                IdempotencyKey = request.IdempotencyKey,
                SampleRequestId = sampleRequest.SampleRequestId,
                ExternalId = sampleRequest.ExternalId,
                ChangedByEmployeeId = employeeId.Value,
                ChangedAt = DateTime.Now,
                Changes = changes
            }
        }, cancellationToken);
    }

    private static IReadOnlyList<SampleRequestDirectPatchChangeDto> NormalizeAndValidateChanges(
        IReadOnlyList<SampleRequestDirectPatchChangeDto>? requestChanges,
        out string? error)
    {
        error = null;

        if (requestChanges is null || requestChanges.Count == 0 || requestChanges.Count > MaxChanges)
        {
            error = $"Changes must contain between 1 and {MaxChanges} fields.";
            return Array.Empty<SampleRequestDirectPatchChangeDto>();
        }

        var result = new List<SampleRequestDirectPatchChangeDto>(requestChanges.Count);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in requestChanges)
        {
            var fieldCode = change.FieldCode?.Trim();
            if (!SampleRequestDirectPatchFieldCatalog.IsSupported(fieldCode))
            {
                error = $"Field '{fieldCode}' is not supported for direct patch notification.";
                return Array.Empty<SampleRequestDirectPatchChangeDto>();
            }

            if (!seenCodes.Add(fieldCode!))
            {
                error = $"Field '{fieldCode}' is duplicated.";
                return Array.Empty<SampleRequestDirectPatchChangeDto>();
            }

            var label = change.Label?.Trim();
            if (string.IsNullOrWhiteSpace(label) || label.Length > MaxLabelLength)
            {
                error = $"Label for field '{fieldCode}' is required and cannot exceed {MaxLabelLength} characters.";
                return Array.Empty<SampleRequestDirectPatchChangeDto>();
            }

            if (change.OldValue.ValueKind == JsonValueKind.Undefined ||
                change.NewValue.ValueKind == JsonValueKind.Undefined)
            {
                error = $"OldValue and NewValue are required for field '{fieldCode}'.";
                return Array.Empty<SampleRequestDirectPatchChangeDto>();
            }

            if (JsonElementEquals(change.OldValue, change.NewValue))
            {
                error = $"Field '{fieldCode}' has not changed.";
                return Array.Empty<SampleRequestDirectPatchChangeDto>();
            }

            result.Add(new SampleRequestDirectPatchChangeDto
            {
                FieldCode = fieldCode!,
                Label = label,
                OldValue = change.OldValue,
                NewValue = change.NewValue
            });
        }

        return result;
    }

    private async Task<SendInternalMessageResultDto?> FindExistingResultAsync(
        Guid sampleRequestId,
        Guid companyId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var conversation = await _dbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId == sampleRequestId)
            .Select(x => new
            {
                x.InternalConversationId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        var messages = await _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == conversation.InternalConversationId &&
                !x.IsDeleted &&
                x.PayloadJson != null)
            .OrderByDescending(x => x.SentAt)
            .Select(x => new
            {
                x.InternalMessageId,
                x.PayloadJson
            })
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            var payload = DeserializePayload(message.PayloadJson);
            if (payload?.DirectPatchNotification is null)
            {
                continue;
            }

            if (payload.DirectPatchNotification.IdempotencyKey == idempotencyKey)
            {
                return new SendInternalMessageResultDto
                {
                    ConversationId = conversation.InternalConversationId,
                    MessageId = message.InternalMessageId,
                    NotificationId = Guid.Empty
                };
            }
        }

        return null;
    }

    private static SampleRequestThreadMessagePayload? DeserializePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SampleRequestThreadMessagePayload>(
                payloadJson,
                PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
        => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);

}
