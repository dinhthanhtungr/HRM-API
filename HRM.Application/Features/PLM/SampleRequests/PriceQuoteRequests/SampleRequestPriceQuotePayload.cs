namespace HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;

public static class SampleRequestPriceQuotePayloadTypes
{
    public const string Request = "SampleRequestPriceQuoteRequested";
}

/// <summary>
/// Structured navigation metadata for a Sample Request price quote request.
/// Pricing values are intentionally excluded because recipients must read live pricing data.
/// </summary>
public sealed class SampleRequestPriceQuotePayload
{
    public Guid SampleRequestId { get; init; }
    public string SampleRequestExternalId { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public Guid? FormulaId { get; init; }
    public string? FormulaExternalId { get; init; }
    public string? FormulaName { get; init; }
    public string FormulaSelectionSource { get; init; } = string.Empty;
    public SampleRequestPriceQuoteActionDto Action { get; init; } = new();
}

public sealed class SampleRequestPriceQuoteActionDto
{
    public string Code { get; init; } = "SampleRequest.OpenPriceQuote";
    public SampleRequestPriceQuoteActionParametersDto Parameters { get; init; } = new();
}

public sealed class SampleRequestPriceQuoteActionParametersDto
{
    public Guid SampleRequestId { get; init; }
    public string SampleRequestExternalId { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public Guid? FormulaId { get; init; }
}
