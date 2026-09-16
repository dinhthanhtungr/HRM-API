using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialReplacements;

/// <summary>
/// Danh sách quản trị phương án thay thế, bao gồm phương án tạm ngưng hoặc NVL thay thế không còn mua được.
/// </summary>
public sealed record GetMaterialReplacementsQuery(Guid SourceMaterialId)
    : IRequest<IReadOnlyList<MaterialReplacementDto>>;
