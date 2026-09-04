using HRM.Domain.Enums.SampleRequests;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.ColorChipRecords.Dtos;

public sealed class ColorChipRecordDto
{
    public Guid ColorChipRecordId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecordType RecordType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResinType ResinType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogoType LogoType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormStyle FormStyle { get; init; }

    public Guid? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductExternalId { get; init; }
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
    public DateTime CreatedDate { get; init; }
    public Guid CreatedBy { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public Guid? UpdatedBy { get; init; }
    public Guid CompanyId { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<ColorChipRecordDevelopmentFormulaDto> DevelopmentFormulas { get; init; } = [];
}

public sealed class ColorChipRecordDevelopmentFormulaDto
{
    public Guid ColorChipRecordDevelopmentFormulaId { get; init; }
    public Guid? DevelopmentFormulaId { get; init; }
    public bool IsActive { get; init; }
    public string? DevelopmentFormulaExternalId { get; init; }
    public string? DevelopmentFormulaName { get; init; }
}

public sealed class SaveColorChipRecordResultDto
{
    public Guid ColorChipRecordId { get; init; }
    public Guid ProductId { get; init; }
    public Guid? AttachmentCollectionId { get; init; }
    public DateTime? UpdatedDate { get; init; }
}
