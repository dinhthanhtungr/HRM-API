using HRM.Application.Features.PLM.Materials.Dtos.Preview;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed record MaterialAttachmentListDto(Guid MaterialId, DateTime? UpdatedDate,
    IReadOnlyList<MaterialPreviewAttachmentDto> Attachments);
