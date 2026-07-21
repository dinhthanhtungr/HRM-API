using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
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

    public GetSampleRequestMessagesQueryHandler(
        IPLMReadDbContext plmDbContext,
        ICurrentUser currentUser)
    {
        _plmDbContext = plmDbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SampleRequestMessageDto>?> Handle(
        GetSampleRequestMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return null;
        }

        var companyId = _currentUser.CompanyId;
        var currentEmployeeId = _currentUser.EmployeeId;

        if (!companyId.HasValue || companyId.Value == Guid.Empty ||
            !currentEmployeeId.HasValue || currentEmployeeId.Value == Guid.Empty)
        {
            return null;
        }

        var sampleRequestExists = await _plmDbContext.SampleRequests
            .AsNoTracking()
            .AnyAsync(x =>
                x.SampleRequestId == request.SampleRequestId &&
                x.CompanyId == companyId.Value &&
                x.IsActive,
                cancellationToken);

        if (!sampleRequestExists)
        {
            return null;
        }

        var conversation = await _plmDbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId.Value &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId == request.SampleRequestId &&
                x.Participants.Any(participant => participant.EmployeeId == currentEmployeeId.Value))
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
                    .Where(state => state.EmployeeId == currentEmployeeId.Value)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadDate = x.ReadStates
                    .Where(state => state.EmployeeId == currentEmployeeId.Value)
                    .Select(state => state.ReadAt)
                    .FirstOrDefault(),
                MessageType = x.MessageType.ToString(),
                ReplyToMessageId = x.ReplyToMessageId
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(ToDto)
            .ToList();
    }

    private static SampleRequestMessageDto ToDto(SampleRequestMessageProjection row)
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
            ReadDate = row.ReadDate
        };
    }

    private static SampleRequestMessagePayload ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new SampleRequestMessagePayload();
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;

            return new SampleRequestMessagePayload
            {
                ConversationId = TryGetGuid(root, "conversationId"),
                MessageId = TryGetGuid(root, "messageId"),
                Type = TryGetString(root, "type"),
                SaleMessage = TryGetString(root, "saleMessage"),
                IsUrgent = TryGetBoolean(root, "isUrgent")
            };
        }
        catch (JsonException)
        {
            return new SampleRequestMessagePayload();
        }
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
