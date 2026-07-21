using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Notifications.Commands.UnsubscribeWebPush;

public sealed class UnsubscribeWebPushCommand : IRequest<OperationResult>
{
    public string Endpoint { get; set; } = string.Empty;
}
