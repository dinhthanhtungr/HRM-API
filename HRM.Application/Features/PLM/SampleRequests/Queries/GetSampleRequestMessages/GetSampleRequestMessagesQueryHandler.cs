using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestMessages.Models;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestMessages;

/// <summary>
/// Doc lich su trao doi cua SampleRequest tu bang InternalMail.
/// Notification chi con dong vai tro inbox/realtime; thread that duoc lay tu InternalConversation/InternalMessage.
/// </summary>
internal sealed class GetSampleRequestMessagesQueryHandler
    : IRequestHandler<GetSampleRequestMessagesQuery, IReadOnlyList<SampleRequestMessageDto>?>
{
    private readonly IPLMReadDbContext _plmDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetSampleRequestMessagesQueryHandler(
        IPLMReadDbContext plmDbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService)
    {
        _plmDbContext = plmDbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
    }

    public async Task<IReadOnlyList<SampleRequestMessageDto>?> Handle(
        GetSampleRequestMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return null;
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var companyId = scope.CompanyId;
        var currentEmployeeId = scope.EmployeeId;

        var sampleRequestQuery = _visibilityService.ApplySampleRequestVisibility(
            _plmDbContext.SampleRequests
                .Where(x => x.SampleRequestId == request.SampleRequestId)
                .AsNoTracking(),
            _plmDbContext.Customers.AsNoTracking(),
            scope);

        var sampleRequestExists = await sampleRequestQuery.AnyAsync(cancellationToken);

        if (!sampleRequestExists)
        {
            return null;
        }

        var conversation = await _plmDbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId == request.SampleRequestId &&
                x.Participants.Any(participant =>
                    participant.EmployeeId == currentEmployeeId && participant.IsActive))
            .Select(x => new
            {
                x.InternalConversationId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return Array.Empty<SampleRequestMessageDto>();
        }

        var rows = await _plmDbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == conversation.InternalConversationId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.SentAt)
            .ThenByDescending(x => x.InternalMessageId)
            .Select(x => new SampleRequestMessageProjection
            {
                ConversationId = x.InternalConversationId,
                MessageId = x.InternalMessageId,
                Title = string.Empty,
                Severity = x.IsUrgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
                Message = x.Body,
                PayloadJson = x.PayloadJson,
                CreatedBy = x.SenderEmployeeId,
                CreatedByName = x.SenderEmployee.FullName,
                CreatedAt = x.SentAt,
                IsRead = x.ReadStates
                    .Where(state => state.EmployeeId == currentEmployeeId)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadDate = x.ReadStates
                    .Where(state => state.EmployeeId == currentEmployeeId)
                    .Select(state => state.ReadAt)
                    .FirstOrDefault(),
                MessageType = x.MessageType.ToString(),
                ReplyToMessageId = x.ReplyToMessageId
            })
            .ToListAsync(cancellationToken);

        var canDecideDataChange = SampleRequestDataChangeAuthorization.CanApprove(_currentUser);
        var canDecideFormulaChange = SampleRequestFormulaChangeAuthorization.CanApproveOrReject(_currentUser);

        return rows
            .Select(row => ToDto(row, canDecideDataChange, canDecideFormulaChange))
            .ToList();
    }

    private static SampleRequestMessageDto ToDto(
        SampleRequestMessageProjection row,
        bool canDecideDataChange,
        bool canDecideFormulaChange)
    {
        var payload = ParsePayload(row.PayloadJson);

        return new SampleRequestMessageDto
        {
            ConversationId = payload.ConversationId ?? row.ConversationId,
            MessageId = payload.MessageId ?? row.MessageId,
            Type = payload.Type ?? string.Empty,
            Title = BuildTitle(payload.Type),
            Severity = row.Severity,
            Message = row.Message,
            IsUrgent = row.Severity == NotificationSeverity.Warning,
            MessageType = row.MessageType ?? string.Empty,
            ReplyToMessageId = row.ReplyToMessageId,
            CreatedBy = row.CreatedBy,
            CreatedByName = row.CreatedByName,
            CreatedAt = row.CreatedAt,
            IsRead = row.IsRead,
            ReadDate = row.ReadDate,
            Action = ToActionDto(payload, canDecideDataChange),
            FormulaChangeAction = ToFormulaChangeActionDto(payload, canDecideFormulaChange),
            DirectPatchNotification = payload.DirectPatchNotification
        };
    }

    private static SampleRequestThreadMessagePayload ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new SampleRequestThreadMessagePayload();
        }

        try
        {
            return JsonSerializer.Deserialize<SampleRequestThreadMessagePayload>(payloadJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new SampleRequestThreadMessagePayload();
        }
        catch (JsonException)
        {
            return new SampleRequestThreadMessagePayload();
        }
    }

    private static SampleRequestDataChangeActionDto? ToActionDto(
        SampleRequestThreadMessagePayload payload,
        bool canDecide)
    {
        var dataChange = payload.DataChangeRequest;
        if (dataChange is null)
        {
            return null;
        }

        return new SampleRequestDataChangeActionDto
        {
            Status = SampleRequestDataChangeStatusRules.GetOverallStatus(dataChange.Changes),
            SampleRequestId = dataChange.SampleRequestId,
            ExternalId = dataChange.ExternalId,
            CanDecide = canDecide && dataChange.Changes.Any(SampleRequestDataChangeStatusRules.IsPending),
            Changes = dataChange.Changes.Select(change => new SampleRequestDataChangeFieldDto
            {
                FieldCode = change.FieldCode,
                Label = change.Label,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                Status = change.Status,
                DecidedByEmployeeId = change.DecidedByEmployeeId,
                DecidedAt = change.DecidedAt,
                DecisionReason = change.DecisionReason
            }).ToArray()
        };
    }

    private static SampleRequestFormulaChangeActionDto? ToFormulaChangeActionDto(
        SampleRequestThreadMessagePayload payload,
        bool canDecide)
    {
        var formulaChange = payload.FormulaChangeRequest;
        if (formulaChange is null)
        {
            return null;
        }

        return new SampleRequestFormulaChangeActionDto
        {
            Status = formulaChange.Status,
            SampleRequestId = formulaChange.SampleRequestId,
            ExternalId = formulaChange.ExternalId,
            CurrentFormulaId = formulaChange.CurrentFormulaId,
            CurrentFormulaExternalId = formulaChange.CurrentFormulaExternalId,
            RequestedFormulaId = formulaChange.RequestedFormulaId,
            RequestedFormulaExternalId = formulaChange.RequestedFormulaExternalId,
            RequestedByEmployeeId = formulaChange.RequestedByEmployeeId,
            RequestedAt = formulaChange.RequestedAt,
            DecidedByEmployeeId = formulaChange.DecidedByEmployeeId,
            DecidedAt = formulaChange.DecidedAt,
            DecisionReason = formulaChange.DecisionReason,
            CanDecide = canDecide &&
                string.Equals(
                    formulaChange.Status,
                    SampleRequestFormulaChangeStatuses.Pending,
                    StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string BuildTitle(string? type)
    {
        return type switch
        {
            nameof(SampleRequestNotificationType.PriceQuoteRequest) => "Yeu cau bao gia mau",
            nameof(SampleRequestNotificationType.ChangeRequest) => "Yeu cau thay doi mau",
            nameof(SampleRequestNotificationType.UpdateRequest) => "Yeu cau cap nhat mau",
            nameof(SampleRequestNotificationType.GeneralMessage) => "Tin nhan ve yeu cau phoi mau",
            _ => "Tin nhan ve yeu cau phoi mau"
        };
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static bool TryGetBoolean(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var value) &&
               value.ValueKind == JsonValueKind.True;
    }

    private static Guid? TryGetGuid(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(value.GetString(), out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
