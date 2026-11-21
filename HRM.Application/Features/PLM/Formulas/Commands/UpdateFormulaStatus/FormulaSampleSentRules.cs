using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;

internal static class FormulaSampleSentRules
{
    private const decimal MaxDeliveredSampleQuantityKg = 99999999999999.9999m;
    internal static bool IsValidLegacyQuantity(decimal? quantity)
        => quantity is > 0 && quantity <= MaxDeliveredSampleQuantityKg;

    public static bool CanReceiveFormulaStatus(string? requestStatus, FormulaStatus targetStatus)
        => requestStatus != "Cancelled" && requestStatus != "LabDeclined" &&
           (requestStatus != "Completed" || targetStatus == FormulaStatus.Approved);

    public static string? ValidateRequest(UpdateFormulaStatusRequest request, bool isChip = false)
    {
        if (request.Status != FormulaStatus.SampleSent)
        {
            return request.DeliveredSampleQuantityKg.HasValue
                ? "DeliveredSampleQuantityKg can only be supplied when status is SampleSent."
                : null;
        }

        if (!request.SampleRequestId.HasValue || request.SampleRequestId.Value == Guid.Empty)
        {
            return "SampleRequestId is required when sending a sample.";
        }

        if (isChip)
            return request.DeliveredSampleQuantityKg is null or 0 ? null : "Chip delivery must not specify kg.";

        if (!request.DeliveredSampleQuantityKg.HasValue ||
            request.DeliveredSampleQuantityKg.Value < 0 ||
            request.DeliveredSampleQuantityKg.Value > MaxDeliveredSampleQuantityKg)
        {
            return "DeliveredSampleQuantityKg must be greater than or equal to 0 and fit the supported precision when sending a sample.";
        }

        return null;
    }

    public static bool CanConfirmFromStatus(string? currentStatus)
        => string.Equals(currentStatus, FormulaStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase) ||
           string.Equals(currentStatus, FormulaStatus.Rework.ToString(), StringComparison.OrdinalIgnoreCase) ||
           string.Equals(currentStatus, FormulaStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase);

    public static bool CanSendFromStatus(string? currentStatus)
        => string.Equals(currentStatus, FormulaStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase) ||
           string.Equals(currentStatus, FormulaStatus.SampleSent.ToString(), StringComparison.OrdinalIgnoreCase);

    public static bool CanRetryFailedKg(SampleRequestSampleTrial? latest, Guid requestId, Guid formulaId)
        => latest is { IsActive: true, Status: SampleTrialStatus.Failed, SentDate: not null } &&
           latest.SampleRequestId == requestId && latest.FormulaId == formulaId &&
           latest.DeliveryKind == SampleDeliveryKind.TrialSample;

    public static void PrepareTrialForDelivery(
        SampleRequestSampleTrial trial,
        Guid formulaId,
        string formulaExternalId,
        Guid currentEmployeeId,
        DateTime now,
        decimal deliveredSampleQuantityKg)
    {
        trial.FormulaId = formulaId;
        trial.BatchNo = formulaExternalId.Trim();
        trial.Status = SampleTrialStatus.WaitingCustomerFeedback;
        trial.DeliveredSampleQuantityKg = deliveredSampleQuantityKg;
        trial.SentDate = now;
        trial.SentByEmployeeId = currentEmployeeId;
        trial.CustomerReplyStatus = "WAITING";
        trial.UpdatedBy = currentEmployeeId;
        trial.UpdatedDate = now;
    }
}
