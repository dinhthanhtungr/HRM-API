using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.PatchBomVersion;

/// <summary>Cập nhật một phần metadata của E-BOM Draft; không thay đổi item.</summary>
public sealed record PatchBomVersionCommand(
    Guid BomVersionId,
    PatchBomVersionRequest Request)
    : IRequest<OperationResult<BomVersionDto>>;
