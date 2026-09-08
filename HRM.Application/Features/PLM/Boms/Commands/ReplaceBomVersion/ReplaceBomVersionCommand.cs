using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ReplaceBomVersion;

/// <summary>Thay thế metadata và toàn bộ item của một E-BOM Draft.</summary>
public sealed record ReplaceBomVersionCommand(
    Guid BomVersionId,
    ReplaceBomVersionRequest Request)
    : IRequest<OperationResult<BomVersionDto>>;
