using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.ColorChipRecords.Commands.CreateColorChipRecord;

/// <summary>
/// Tạo hồ sơ Color Chip cho một Product thuộc công ty hiện tại; backend quản lý tenant, audit và attachment collection.
/// </summary>
public sealed class CreateColorChipRecordCommand : IRequest<OperationResult<SaveColorChipRecordResultDto>>
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecordType RecordType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResinType ResinType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogoType LogoType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormStyle FormStyle { get; init; }

    public Guid ProductId { get; init; }
    public string? Machine { get; init; }
    public string? Resin { get; init; }
    public string? TemperatureLimit { get; init; }
    public string? SizeText { get; init; }
    public decimal? PelletWeightGram { get; init; }
    public string? NetWeightGram { get; init; }
    public bool? Electrostatic { get; init; }
    public decimal Lightness { get; init; }
    public decimal AValue { get; init; }
    public decimal BValue { get; init; }
    public Guid? AttachmentCollectionId { get; init; }
    public DateTime? RecordDate { get; init; }
    public string? Note { get; init; }
    public string? PrintNote { get; init; }
    public IReadOnlyList<Guid> DevelopmentFormulaIds { get; init; } = [];
}
