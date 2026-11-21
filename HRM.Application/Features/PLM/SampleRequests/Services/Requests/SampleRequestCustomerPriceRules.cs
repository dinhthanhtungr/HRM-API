using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

public static class SampleRequestCustomerPriceRules
{
    public static bool IsQuotation(string? infoType) =>
        string.Equals(infoType?.Trim(), "Quotation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(infoType?.Trim(), "Báo giá", StringComparison.OrdinalIgnoreCase);

    public static bool IsColourSet(string? infoType)
    {
        var type = infoType?.Trim() ?? string.Empty;
        return type.StartsWith("Thẻ màu", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("ColorSampleSet", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("ColourSampleSet", StringComparison.OrdinalIgnoreCase);
    }

    // Tương thích tên helper cũ: chỉ nhận diện trạng thái theo dõi giá, không dùng để khóa gửi mẫu.
    public static bool IsBlocked(SampleRequest request) =>
        string.Equals(request.Status, "WaitingCustomerPriceConfirmation", StringComparison.OrdinalIgnoreCase);
    public static string ResolveSubmittedStatus(string initialStatus, string? infoType) =>
        // Loại yêu cầu không được bỏ qua điều kiện đủ tên và mã màu.
        initialStatus;

    public static string ResolveStatusAfterPriceEvent(string currentStatus, bool revoke) =>
        !revoke && string.Equals(currentStatus, "WaitingCustomerPriceConfirmation", StringComparison.OrdinalIgnoreCase)
            ? "InProgress" : currentStatus;

    public static string? ValidateStatusPatch(string currentStatus, string? requestedStatus)
    {
        if (string.Equals(currentStatus, "LabDeclined", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedStatus?.Trim(), "LabDeclined", StringComparison.OrdinalIgnoreCase))
            return "Use the Lab refusal or additional-work action to change a Lab-declined request.";
        if (requestedStatus is null || string.Equals(currentStatus, requestedStatus.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(requestedStatus.Trim(), "WaitingCustomerPriceConfirmation", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(currentStatus, "WaitingCustomerPriceConfirmation", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(requestedStatus.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(requestedStatus.Trim(), "SampleSent", StringComparison.OrdinalIgnoreCase)))
            return "Use the customer price confirmation action to transfer this request to Lab or revoke acceptance.";
        return null;
    }
}
