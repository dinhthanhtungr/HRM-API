using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialReplacementOptions;

/// <summary>
/// Các phương án đang có thể dùng để thay NVL nguồn trong công thức.
/// </summary>
public sealed record GetMaterialReplacementOptionsQuery(Guid SourceMaterialId)
    : IRequest<IReadOnlyList<MaterialReplacementDto>>;
