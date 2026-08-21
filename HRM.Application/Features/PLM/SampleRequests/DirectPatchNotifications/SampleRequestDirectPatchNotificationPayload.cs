using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Features.PLM.SampleRequests.DirectPatchNotifications;

internal static class SampleRequestDirectPatchNotificationPayloadTypes
{
    public const string Notification = "SampleRequestDirectPatchNotification";
}

public sealed class SampleRequestDirectPatchNotificationPayload
{
    public Guid IdempotencyKey { get; set; }
    public Guid SampleRequestId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public Guid ChangedByEmployeeId { get; set; }
    public DateTime ChangedAt { get; set; }
    public IReadOnlyList<SampleRequestDirectPatchChangeDto> Changes { get; set; }
        = Array.Empty<SampleRequestDirectPatchChangeDto>();
}

public sealed class SampleRequestDirectPatchChangeDto
{
    public string FieldCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public JsonElement OldValue { get; set; }
    public JsonElement NewValue { get; set; }
}

internal static class SampleRequestDirectPatchFieldCatalog
{
    private static readonly IReadOnlySet<string> DirectNotifyFieldCodes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "sample_request.expected_quantity",
        "sample_request.expected_price",
        "sample_request.sample_quantity",
        "sample_request.number_delivery_sample_date",
        "sample_request.package",
        "sample_request.bag_weight",
        "sample_request.customer_product_code",
        "sample_request.request_delivery_date",
        "sample_request.expected_delivery_date",
        "sample_request.real_delivery_date",
        "sample_request.request_test_sample_date",
        "sample_request.response_delivery_date",
        "sample_request.expected_price_quote_date",
        "sample_request.real_price_quote_date",
        "sample_request.info_type",
        "sample_request.other_comment",
        "sample_request.sale_comment",
        "sample_request.additional_comment",
        "sample_request.request_type",

        "product.colour_code",
        "product.name",
        "product.colour_name",
        "product.additive",
        "product.usage_rate",
        "product.delta_e",
        "product.requirement",
        "product.expiry_type",
        "product.storage_condition",
        "product.lab_comment",
        "product.procedure",
        "product.recycle_rate",
        "product.taical_rate",
        "product.application",
        "product.product_usage",
        "product.polymer_matched_in",
        "product.code",
        "product.end_user",
        "product.max_temp",
        "product.weather_resistance",
        "product.light_condition",
        "product.visual_test",
        "product.return_sample",
        "product.is_recycle",
        "product.weight",
        "product.unit",
        "product.other_comment"
    };

    public static bool IsSupported(string? fieldCode)
        => !string.IsNullOrWhiteSpace(fieldCode) &&
            !SampleRequestLabApprovalRules.IsRequiredApprovalField(fieldCode) &&
            DirectNotifyFieldCodes.Contains(fieldCode.Trim());
}
