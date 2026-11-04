using HRM.Application.Commons.Models;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationPendingSendConfirmationDto
{
    public Guid RequestMessageId { get; init; }
    public DateTime RequestedAt { get; init; }
    public string RequesterName { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public int ComparisonRuleVersion { get; init; }
    public IReadOnlyList<QuotationSendPriceSegment> Comparison { get; init; } = [];
}

public sealed class QuotationConfirmationFailure : OperationResult
{
    public string Code { get; init; } = string.Empty;
    public static QuotationConfirmationFailure Create(string code, string message)
        => new() { Success = false, Code = code, Message = message };
}

public static class QuotationConfirmationErrors
{
    public const string TokenRequired = "quotation.confirm.token_required";
    public const string InvalidRequest = "quotation.confirm.invalid_request";
    public const string Forbidden = "quotation.confirm.forbidden";
    public const string NotFound = "quotation.confirm.not_found";
    public const string Conflict = "quotation.confirm.conflict";
    public const string NotPending = "quotation.confirm.not_pending";
    public const string NoPendingRequest = "quotation.confirm.no_pending_request";
    public const string AmbiguousRequests = "quotation.confirm.ambiguous_requests";
    public const string RequestOutdated = "quotation.confirm.request_outdated";
    public const string SnapshotOutdated = "quotation.confirm.snapshot_outdated";
    public const string CommercialChanged = "quotation.confirm.commercial_changed";
    public const string StandardUnavailable = "quotation.confirm.standard_unavailable";
}
