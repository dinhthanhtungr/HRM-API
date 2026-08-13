using HRM.Application.Commons.Models;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestSampleTrial;

/// <summary>
/// Tạo một lần thử/gửi mẫu mới; backend tự cấp TrialNo và snapshot dữ liệu báo cáo.
/// </summary>
public sealed class CreateSampleRequestSampleTrialCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore]
    public Guid SampleRequestId { get; set; }

    public Guid? FormulaId { get; set; }
    public string? FormulaExternalId { get; set; }
    public string? BatchNo { get; set; }
    public decimal? DeliveredSampleQuantityKg { get; set; }
    public decimal? AdditiveRate { get; set; }
    public DateTime? RequestReceivedDate { get; set; }
    public DateTime? FinishedDate { get; set; }
    public DateTime? SentDate { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? LabNote { get; set; }
    public Guid? SentByEmployeeId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleTrialStatus Status { get; set; } = SampleTrialStatus.Draft;
}
