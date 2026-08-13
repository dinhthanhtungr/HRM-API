using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;

internal static class FormulaSampleSentRules
{
    private const decimal MaxDeliveredSampleQuantityKg = 99999999999999.9999m;

    public static string? ValidateRequest(UpdateFormulaStatusRequest request)
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

        if (!request.DeliveredSampleQuantityKg.HasValue ||
            request.DeliveredSampleQuantityKg.Value < 0 ||
            request.DeliveredSampleQuantityKg.Value > MaxDeliveredSampleQuantityKg)
        {
            return "DeliveredSampleQuantityKg must be greater than or equal to 0 and fit the supported precision when sending a sample.";
        }

        return null;
    }

    public static bool CanSendFromStatus(string? currentStatus)
        => string.Equals(currentStatus, FormulaStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase) ||
           string.Equals(currentStatus, FormulaStatus.SampleSent.ToString(), StringComparison.OrdinalIgnoreCase);
}
