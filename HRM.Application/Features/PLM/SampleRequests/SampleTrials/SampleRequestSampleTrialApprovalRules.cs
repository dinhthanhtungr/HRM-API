using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleRequestSampleTrialApprovalRules
{
    public const string ApprovedCustomerReplyStatus = "APPROVED";

    public static string? Validate(
        SampleRequestSampleTrial? trial,
        Guid selectedFormulaId)
    {
        if (trial is null)
        {
            return "No active sample trial is waiting for customer feedback.";
        }

        if (trial.FormulaId is not { } trialFormulaId ||
            trialFormulaId == Guid.Empty ||
            trial.Formula is null)
        {
            return "Sample trial does not have a formula to complete.";
        }

        if (trialFormulaId != selectedFormulaId)
        {
            return "Selected formula does not match the latest sample trial awaiting customer feedback.";
        }

        if (trial.Status is not SampleTrialStatus.SampleSent and
            not SampleTrialStatus.WaitingCustomerFeedback and
            not SampleTrialStatus.PriceQuote)
        {
            return "Customer acceptance can only be recorded for a sent sample awaiting customer feedback.";
        }

        if (!string.Equals(
                trial.Formula.Status,
                FormulaStatus.SampleSent.ToString(),
                StringComparison.OrdinalIgnoreCase))
        {
            return "Only a SampleSent formula can be completed from customer feedback.";
        }

        return null;
    }

    public static void ApplyApproved(
        SampleRequest sampleRequest,
        SampleRequestSampleTrial trial,
        IReadOnlyCollection<Formula> productFormulas,
        Guid employeeId,
        DateTime now,
        string customerReplyStatus,
        string? customerReplyNote = null,
        DateTime? customerReplyDate = null)
    {
        var formulaId = trial.FormulaId!.Value;

        trial.Status = SampleTrialStatus.Approved;
        trial.CustomerReplyStatus = customerReplyStatus;
        trial.CustomerReplyDate = customerReplyDate ?? now;
        trial.CustomerReplyByEmployeeId = employeeId;
        trial.CustomerReplyNote = customerReplyNote;
        trial.UpdatedBy = employeeId;
        trial.UpdatedDate = now;

        foreach (var formula in productFormulas)
        {
            formula.IsSelect = formula.FormulaId == formulaId;
        }

        trial.Formula!.Status = FormulaStatus.Completed.ToString();
        trial.Formula.UpdatedBy = employeeId;
        trial.Formula.UpdatedDate = now;

        sampleRequest.FormulaId = formulaId;
        SampleRequestStatusTransitionRules.MarkCustomerApproved(sampleRequest);
    }
}
