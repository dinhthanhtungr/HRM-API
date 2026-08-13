using HRM.Application.Commons.Models;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequestSampleTrial;

/// <summary>
/// Cập nhật một trial. Field không gửi được giữ nguyên; field trong ClearFields được gán null.
/// </summary>
public sealed class PatchSampleRequestSampleTrialCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore]
    public Guid SampleRequestId { get; set; }

    [JsonIgnore]
    public Guid SampleRequestSampleTrialId { get; set; }

    public DateTime? ExpectedUpdatedDate { get; set; }
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
    public SampleTrialStatus? Status { get; set; }

    public IReadOnlyList<string>? ClearFields { get; set; }
}
