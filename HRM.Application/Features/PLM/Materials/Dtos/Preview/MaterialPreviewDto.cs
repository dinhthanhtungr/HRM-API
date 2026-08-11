using System.Text.Json.Serialization;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.Materials.Dtos.Preview;

/// <summary>
/// Thông tin nhẹ của NVL và tệp đính kèm dùng cho cửa sổ xem nhanh trên UI.
/// </summary>
public sealed class MaterialPreviewDto
{
    public Guid MaterialId { get; init; }
    public string? ExternalId { get; init; }
    public string? CustomCode { get; init; }
    public string? Name { get; init; }
    public string? CategoryName { get; init; }
    public decimal TotalOnHandKg { get; init; }
    public MaterialLastPurchaseDto? LastPurchase { get; init; }
    public int AttachmentCount => Attachments.Count;
    public IReadOnlyList<MaterialPreviewAttachmentDto> Attachments { get; init; } = [];
}

/// <summary>
/// Lần mua hợp lệ gần nhất của NVL; đơn giá chỉ trả cho user có quyền xem giá.
/// </summary>
public sealed class MaterialLastPurchaseDto
{
    public Guid PurchaseOrderId { get; init; }
    public string? PurchaseOrderCode { get; init; }
    public string? SupplierName { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? Quantity { get; init; }
    public DateTime? PurchaseDate { get; init; }
}

/// <summary>
/// Metadata của một tệp thuộc NVL; nội dung file được tải riêng qua ContentUrl.
/// </summary>
public sealed class MaterialPreviewAttachmentDto
{
    public Guid AttachmentId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttachmentSlot Slot { get; init; }

    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public bool IsImage { get; init; }
    public bool IsPdf { get; init; }
    public string ContentUrl { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
}
