using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using MediatR;

namespace HRM.Application.Features.Notifications.Queries.GetWebPushPublicKey;

public sealed record GetWebPushPublicKeyQuery : IRequest<OperationResult<WebPushPublicKeyDto>>;
