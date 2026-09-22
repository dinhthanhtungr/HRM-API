using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Comparison;

public sealed class CompareFormulasRequest
{
    public Guid BaseFormulaId { get; init; }
    public Guid ComparedFormulaId { get; init; }
    public string? Currency { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormulaComparisonStatus
{
    Increased = 0,
    Decreased = 10,
    Unchanged = 20,
    Unavailable = 30
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormulaComparisonItemStatus
{
    Unchanged = 0,
    QuantityChanged = 10,
    AddedToComparedFormula = 20,
    RemovedFromComparedFormula = 30,
    MissingCurrentPrice = 40,
    UnitMismatch = 50
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormulaComparisonUnavailableReason
{
    MissingCurrentPrice = 0,
    UnitMismatch = 10
}

public sealed class FormulaComparisonDto
{
    public string Currency { get; init; } = "VND";
    public FormulaComparisonFormulaDto BaseFormula { get; init; } = new();
    public FormulaComparisonFormulaDto ComparedFormula { get; init; } = new();
    public FormulaComparisonSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<FormulaComparisonItemDto> Items { get; init; } = [];
}

public sealed class FormulaComparisonFormulaDto
{
    public Guid FormulaId { get; init; }
    public Guid ProductId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public StepOfProduct? StepOfProduct { get; init; }
}

public sealed class FormulaComparisonSummaryDto
{
    public string PriceBasis { get; init; } = "CurrentResolvedPrice";
    public DateTime CalculatedAt { get; init; }
    public decimal? BaseFormulaMaterialCost { get; init; }
    public decimal? ComparedFormulaMaterialCost { get; init; }
    public decimal? DifferenceAmount { get; init; }
    public decimal? DifferencePercent { get; init; }
    public FormulaComparisonStatus ComparisonStatus { get; init; }
    public int BaseItemCount { get; init; }
    public int ComparedItemCount { get; init; }
    public int MatchedItemCount { get; init; }
    public int AddedToComparedCount { get; init; }
    public int RemovedFromComparedCount { get; init; }
    public int QuantityChangedCount { get; init; }
    public int UnchangedCount { get; init; }
    public int MissingPriceCount { get; init; }
    public int UnitMismatchCount { get; init; }
    public bool CanCompare { get; init; }
    public FormulaComparisonUnavailableReason? UnavailableReason { get; init; }
}

public sealed class FormulaComparisonCurrentPriceDto
{
    public decimal? UnitPrice { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LatestPriceSourceType PriceSource { get; init; } = LatestPriceSourceType.Unknown;

    public DateTime? PriceDate { get; init; }
    public PriceCalculationDetailDto? Calculation { get; init; }
}

public sealed class FormulaComparisonSideDto
{
    public IReadOnlyList<Guid> FormulaMaterialIds { get; init; } = [];
    public bool IsPresent { get; init; }
    public decimal Quantity { get; init; }
    public decimal? Amount { get; init; }
}

public sealed class FormulaComparisonItemDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemType ItemType { get; init; }

    public Guid? MaterialId { get; init; }
    public Guid? ProductId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string Unit { get; init; } = string.Empty;
    public FormulaComparisonCurrentPriceDto CurrentPrice { get; init; } = new();
    public FormulaComparisonSideDto BaseFormula { get; init; } = new();
    public FormulaComparisonSideDto ComparedFormula { get; init; } = new();
    public decimal QuantityDifference { get; init; }
    public decimal? AmountDifference { get; init; }
    public decimal? DifferencePercent { get; init; }
    public FormulaComparisonItemStatus Status { get; init; }
}
