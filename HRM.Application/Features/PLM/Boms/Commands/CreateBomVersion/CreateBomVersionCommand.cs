using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateBomVersion;

public sealed record CreateBomVersionCommand(
    Guid BomDefinitionId,
    CreateBomVersionRequest Request)
    : IRequest<OperationResult<BomVersionDto>>;
