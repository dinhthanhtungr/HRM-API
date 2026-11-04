using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Dispatch.Deliverers.Commands.CreateDeliverer;

public sealed record CreateDelivererCommand(string Name, string? DelivererType, string? Phone, string? Note)
    : IRequest<OperationResult<Guid>>;
