using HRM.Application.Features.PLM.Materials.Dtos.Preview;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialPreview;

/// <summary>
/// Lấy thông tin NVL và metadata tệp đính kèm để UI hiển thị khi xem nhanh.
/// </summary>
public sealed record GetMaterialPreviewQuery(Guid MaterialId)
    : IRequest<MaterialPreviewDto?>;
