using HRM.Application.Abstractions.Notifications;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using MediatR;

namespace HRM.Application.Features.Notifications.Queries.GetWebPushPublicKey;

internal sealed class GetWebPushPublicKeyQueryHandler
    : IRequestHandler<GetWebPushPublicKeyQuery, OperationResult<WebPushPublicKeyDto>>
{
    private readonly IWebPushSender _webPushSender;

    public GetWebPushPublicKeyQueryHandler(IWebPushSender webPushSender)
    {
        _webPushSender = webPushSender;
    }

    public Task<OperationResult<WebPushPublicKeyDto>> Handle(
        GetWebPushPublicKeyQuery request,
        CancellationToken cancellationToken)
    {
        if (!_webPushSender.IsEnabled || string.IsNullOrWhiteSpace(_webPushSender.PublicKey))
        {
            return Task.FromResult(OperationResult<WebPushPublicKeyDto>.Fail("Web Push is not configured."));
        }

        return Task.FromResult(OperationResult<WebPushPublicKeyDto>.Ok(new WebPushPublicKeyDto
        {
            PublicKey = _webPushSender.PublicKey
        }));
    }
}
