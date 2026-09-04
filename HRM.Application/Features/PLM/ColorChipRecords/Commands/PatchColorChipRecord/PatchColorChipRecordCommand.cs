using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.ColorChipRecords.Commands.PatchColorChipRecord;

/// <summary>
/// Cập nhật từng phần hồ sơ Color Chip. Field không gửi được giữ nguyên; field nullable chỉ được xóa qua ClearFields.
/// </summary>
public sealed class PatchColorChipRecordCommand : IRequest<OperationResult<SaveColorChipRecordResultDto>>
{
    [JsonIgnore]
    public Guid ColorChipRecordId { get; set; }

    public DateTime? ExpectedUpdatedDate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecordType? RecordType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResinType? ResinType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogoType? LogoType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormStyle? FormStyle { get; init; }

    public string? Machine { get; init; }
    public string? Resin { get; init; }
    public string? TemperatureLimit { get; init; }
    public string? SizeText { get; init; }
    public decimal? PelletWeightGram { get; init; }
    public string? NetWeightGram { get; init; }
    public bool? Electrostatic { get; init; }
    public decimal? Lightness { get; init; }
    public decimal? AValue { get; init; }
    public decimal? BValue { get; init; }
    public DateTime? RecordDate { get; init; }
    public string? Note { get; init; }
    public string? PrintNote { get; init; }

    /// <summary>
    /// Null giữ nguyên liên kết; mảng rỗng xóa liên kết; mảng một phần tử thay thế development formula.
    /// </summary>
    public IReadOnlyList<Guid>? DevelopmentFormulaIds { get; init; }

    public IReadOnlyList<string>? ClearFields { get; init; }
}
