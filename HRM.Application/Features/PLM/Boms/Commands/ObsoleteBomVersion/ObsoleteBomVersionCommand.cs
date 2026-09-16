using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ObsoleteBomVersion;

public sealed record ObsoleteBomVersionCommand(Guid BomVersionId, string? Reason)
    : IRequest<OperationResult>;
