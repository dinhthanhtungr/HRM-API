using System.Text.Json.Serialization;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Dtos;

/// <summary>
/// Mot dong conversation trong hom thu tong. Danh sach duoc gop theo conversation, khong gop/xoa message that.
/// </summary>
public sealed class InternalConversationListItemDto
{
    public Guid GroupConversationId { get; set; }
    public string AreaCode { get; set; } = "general";
    public Guid ConversationId { get; set; }
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Tiêu đề ngắn dành cho danh sách inbox; Subject đầy đủ vẫn dùng ở màn hình chi tiết.
    /// </summary>
    public string DisplayTitle { get; set; } = string.Empty;

    /// <summary>
    /// Context hien thi tu Sample Request khi conversation lien ket voi yeu cau phoi mau;
    /// null voi cac conversation loai khac hoac khi record nguon khong con duoc phep hien thi.
    /// </summary>
    public SampleRequestConversationInfoDto? SampleRequestInfo { get; set; }

    /// <summary>
    /// Context hiển thị từ báo giá khi conversation liên kết với báo giá;
    /// null với các conversation loại khác hoặc khi record nguồn không còn được phép hiển thị.
    /// </summary>
    public QuotationConversationInfoDto? QuotationInfo { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalMailRelatedType? RelatedType { get; set; }

    public Guid? RelatedId { get; set; }
    public string? RelatedExternalId { get; set; }
    public Guid? LastMessageId { get; set; }
    public string? LastMessageBody { get; set; }
    public Guid? LastSenderEmployeeId { get; set; }
    public string? LastSenderName { get; set; }
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsUrgent { get; set; }
    public bool IsArchived { get; set; }
    public bool IsMuted { get; set; }
}

public sealed class InternalConversationDetailDto
{
    public Guid GroupConversationId { get; set; }
    public string AreaCode { get; set; } = "general";
    public bool? CanManageParticipants { get; set; }
    public IReadOnlyList<InternalMailAreaDto> Areas { get; set; } = Array.Empty<InternalMailAreaDto>();
    public Guid ConversationId { get; set; }
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Context hien thi tu Sample Request khi conversation lien ket voi yeu cau phoi mau;
    /// null voi cac conversation loai khac hoac khi record nguon khong con duoc phep hien thi.
    /// </summary>
    public SampleRequestConversationInfoDto? SampleRequestInfo { get; set; }

    /// <summary>
    /// Context hiển thị từ báo giá khi conversation liên kết với báo giá;
    /// null với các conversation loại khác hoặc khi record nguồn không còn được phép hiển thị.
    /// </summary>
    public QuotationConversationInfoDto? QuotationInfo { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalMailRelatedType? RelatedType { get; set; }

    public Guid? RelatedId { get; set; }
    public string? RelatedExternalId { get; set; }
    public Guid CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public Guid? LastMessageId { get; set; }
    public int UnreadCount { get; set; }
    public bool IsArchived { get; set; }
    public bool IsMuted { get; set; }
    public DateTime? LastReadAt { get; set; }
    public IReadOnlyList<InternalConversationParticipantDto> Participants { get; set; } = Array.Empty<InternalConversationParticipantDto>();
}

public sealed class InternalConversationParticipantDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalConversationParticipantRole Role { get; set; }

    public DateTime JoinedAt { get; set; }
}

/// <summary>
/// Metadata hien thi dong bo cho conversation bam vao Sample Request.
/// Sale la <c>SampleRequest.ManagerBy</c>, khong phai nguoi gui message cuoi.
/// </summary>
public sealed class SampleRequestConversationInfoDto
{
    public Guid SampleRequestId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string? ColourCode { get; set; }
    /// <summary>
    /// Loại sản phẩm hiện tại; null khi sản phẩm/danh mục không còn active hoặc không cùng company.
    /// </summary>
    public ConversationProductCategoryDto? ProductCategory { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid SaleEmployeeId { get; set; }
    public string SaleName { get; set; } = string.Empty;
}

public sealed class ConversationProductCategoryDto
{
    public Guid CategoryId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Metadata hiển thị đồng bộ cho conversation bám vào báo giá.
/// Sale là <c>Quotation.SaleEmployee</c>, không phải người gửi message cuối.
/// </summary>
public sealed class QuotationConversationInfoDto
{
    public Guid QuotationId { get; set; }
    public string QuotationCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid SaleEmployeeId { get; set; }
    public string SaleName { get; set; } = string.Empty;
}
