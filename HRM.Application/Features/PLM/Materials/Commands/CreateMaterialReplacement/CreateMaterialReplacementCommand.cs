using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Commands.CreateMaterialReplacement;

public sealed record CreateMaterialReplacementCommand(
    Guid SourceMaterialId,
    CreateMaterialReplacementRequest Request)
    : IRequest<MaterialReplacementWriteResult>;
