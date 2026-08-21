using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>
/// Gom các quy tắc chuyển trạng thái thuần của Sample Request.
/// Persistence, audit và notification vẫn thuộc handler gọi rule.
/// </summary>
internal static class SampleRequestStatusTransitionRules
{
    public static string ResolveInitialStatus(Product? product)
        => HasProductIdentity(product)
            ? SampleRequestStatus.InProgress.ToString()
            : SampleRequestStatus.New.ToString();

    public static bool TryStartProcessing(SampleRequest sampleRequest, Product? product)
    {
        if (!IsStatus(sampleRequest.Status, SampleRequestStatus.New) || !HasProductIdentity(product))
        {
            return false;
        }

        sampleRequest.Status = SampleRequestStatus.InProgress.ToString();
        return true;
    }

    public static void MarkSampleSent(SampleRequest sampleRequest)
        => sampleRequest.Status = SampleRequestStatus.SampleSent.ToString();

    public static void MarkCustomerApproved(SampleRequest sampleRequest)
        => sampleRequest.Status = SampleRequestStatus.Completed.ToString();

    public static void MarkCustomerFailed(SampleRequest sampleRequest)
        => sampleRequest.Status = SampleRequestStatus.InProgress.ToString();

    public static void MarkCustomerCancelled(SampleRequest sampleRequest)
        => sampleRequest.Status = SampleRequestStatus.Cancelled.ToString();

    public static void MarkFormulaUpdateRequested(SampleRequest sampleRequest)
        => sampleRequest.Status = SampleRequestStatus.FormulaUpdateRequested.ToString();

    public static void MarkFormulaUpdateDecided(SampleRequest sampleRequest)
        => sampleRequest.Status = SampleRequestStatus.Completed.ToString();

    // Giữ tương thích PATCH hiện có; lifecycle status vẫn được command kiểm tra trước khi gọi.
    public static void ApplyDirectPatchStatus(SampleRequest sampleRequest, string? status)
        => sampleRequest.Status = status?.Trim() ?? string.Empty;

    public static bool IsStatus(string? currentStatus, SampleRequestStatus expectedStatus)
        => string.Equals(
            currentStatus?.Trim(),
            expectedStatus.ToString(),
            StringComparison.OrdinalIgnoreCase);

    private static bool HasProductIdentity(Product? product)
        => product is not null &&
           !string.IsNullOrWhiteSpace(product.Name) &&
           !string.IsNullOrWhiteSpace(product.ColourCode);
}
