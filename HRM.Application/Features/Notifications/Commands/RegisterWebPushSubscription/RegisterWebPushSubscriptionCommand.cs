using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using MediatR;

namespace HRM.Application.Features.Notifications.Commands.RegisterWebPushSubscription;

/// <summary>
/// Dang ky/upsert browser subscription cho current employee. FE khong duoc truyen CompanyId hoac EmployeeId.
/// </summary>
public sealed class RegisterWebPushSubscriptionCommand
    : IRequest<OperationResult<WebPushSubscriptionDto>>
{
    public string Endpoint { get; set; } = string.Empty;
    public WebPushSubscriptionKeysDto Keys { get; set; } = new();
    public string? DeviceName { get; set; }

    [JsonIgnore]
    public string? UserAgent { get; set; }
}
