using System.Text.Json;
using System.Text.Json.Serialization;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons;

public sealed class UpsertFormulaRequest
{
    public string? ExternalId { get; init; }
    public string? Name { get; init; }

    [JsonConverter(typeof(NullableGuidEmptyStringConverter))]
    public Guid? ProductId { get; init; }

    public string? Note { get; init; }

    public StepOfProduct? StepOfProduct { get; init; }
    public DateTime? EffectiveDate { get; init; }
    public bool? IsSelect { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
    public IReadOnlyList<UpsertFormulaMaterialRequest>? Materials { get; init; }
}

public sealed class UpsertFormulaMaterialRequest
{
    public Guid? FormulaMaterialId { get; init; }
    public int LineNo { get; init; }
    public Guid ItemId { get; init; }
    public ItemType ItemType { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public string? Unit { get; init; }
    public string? MaterialNameSnapshot { get; init; }
    public string? MaterialExternalIdSnapshot { get; init; }
}

public sealed class UpdateFormulaStatusRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaStatus Status { get; init; }

    [JsonConverter(typeof(NullableGuidEmptyStringConverter))]
    public Guid? SampleRequestId { get; init; }

    public decimal? DeliveredSampleQuantityKg { get; init; }

    /// <summary>
    /// Optional production route update. When omitted, the existing Formula value is retained.
    /// </summary>
    public StepOfProduct? StepOfProduct { get; init; }

    public DateTime? ExpectedUpdatedDate { get; init; }

    /// <summary>
    /// Optional Formula changes to persist atomically before the status transition.
    /// The request order of FormulaUpdate.Materials is the canonical material order.
    /// </summary>
    public UpsertFormulaRequest? FormulaUpdate { get; init; }
}

public sealed class FormulaWriteResultDto
{
    public Guid FormulaId { get; init; }
    public Guid? SampleRequestSampleTrialId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int UpdatedSampleRequestCount { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

internal sealed class NullableGuidEmptyStringConverter : JsonConverter<Guid?>
{
    public override Guid? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Guid.TryParse(value, out var guid))
            {
                return guid;
            }
        }

        throw new JsonException("ProductId must be a valid GUID.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Guid? value,
        JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value);
            return;
        }

        writer.WriteNullValue();
    }
}
