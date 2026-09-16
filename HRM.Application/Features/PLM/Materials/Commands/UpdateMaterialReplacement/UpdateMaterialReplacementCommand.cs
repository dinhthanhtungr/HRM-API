using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialReplacement;

public sealed record UpdateMaterialReplacementCommand(
    Guid MaterialReplacementId,
    UpdateMaterialReplacementRequest Request)
    : IRequest<MaterialReplacementWriteResult>;
