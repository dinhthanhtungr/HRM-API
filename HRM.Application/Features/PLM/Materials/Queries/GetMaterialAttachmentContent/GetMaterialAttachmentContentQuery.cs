using HRM.Application.Features.Attachments.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialAttachmentContent;

/// <summary>
/// Mở nội dung tệp sau khi xác thực tệp thuộc NVL active trong công ty hiện tại.
/// </summary>
public sealed record GetMaterialAttachmentContentQuery(
    Guid MaterialId,
    Guid AttachmentId)
    : IRequest<AttachmentContent?>;
