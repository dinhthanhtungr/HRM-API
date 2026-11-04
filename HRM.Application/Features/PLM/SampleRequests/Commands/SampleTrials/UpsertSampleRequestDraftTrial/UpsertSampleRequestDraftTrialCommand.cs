using HRM.Application.Commons.Models;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpsertSampleRequestDraftTrial;

/// <summary>
/// Tạo Trial Draft đầu tiên khi Lab lưu thông tin kỹ thuật, hoặc cập nhật Draft hiện có theo semantics PATCH.
/// </summary>
public sealed class UpsertSampleRequestDraftTrialCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore]
    public Guid SampleRequestId { get; set; }

    public Guid? FormulaId { get; set; }
    public string? FormulaExternalId { get; set; }
    public string? BatchNo { get; set; }
    public decimal? DeliveredSampleQuantityKg { get; set; }

    /// <summary>Ngày Sale yêu cầu có mẫu; dữ liệu được lưu trên Sample Request cha.</summary>
    public DateTime? RequestDeliveryDate { get; set; }

    /// <summary>Ngày dự kiến có mẫu; dữ liệu được lưu trên Sample Request cha.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    public string? DeliveryMethod { get; set; }
    public string? LabNote { get; set; }

    /// <summary>Concurrency token của Draft hiện có; không bắt buộc trong lần tạo đầu tiên.</summary>
    public DateTime? ExpectedUpdatedDate { get; set; }

    /// <summary>Các field nullable cần xóa; field không gửi được giữ nguyên.</summary>
    public IReadOnlyCollection<string>? ClearFields { get; set; }
}
