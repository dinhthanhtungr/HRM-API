using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ReleaseBomVersion;

public sealed record ReleaseBomVersionCommand(Guid BomVersionId)
    : IRequest<OperationResult>;
