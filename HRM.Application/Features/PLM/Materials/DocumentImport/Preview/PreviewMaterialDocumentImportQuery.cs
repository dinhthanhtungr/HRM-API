using HRM.Application.Features.PLM.Materials.DocumentImport.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Preview;

/// <summary>
/// Quét metadata tệp TDS/MSDS từ nguồn cấu hình và đối chiếu mã với NVL active
/// trong công ty hiện tại. Query không ghi database và không thay đổi tệp nguồn.
/// </summary>
public sealed record PreviewMaterialDocumentImportQuery
    : IRequest<MaterialDocumentImportPreviewDto?>;
