using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;

internal static class SampleTrialInteractionMutation
{
    public static void ApplyCustomerReply(
        SampleRequestSampleTrial trial,
        CreateSampleTrialInteractionRequest request,
        DateTime interactionAt,
        Guid employeeId,
        DateTime now)
    {
        var replyStatus = Normalize(request.CustomerReplyStatus);
        if (replyStatus is not null)
        {
            trial.CustomerReplyStatus = replyStatus;
        }
        trial.CustomerReplyDate = request.CustomerReplyDate ?? interactionAt;
        trial.CustomerReplyByEmployeeId = employeeId;
        trial.CustomerReplyNote = Normalize(request.CustomerReplyNote) ?? request.Content.Trim();
        trial.OrderDate = request.OrderDate ?? trial.OrderDate;
        trial.UpdatedDate = now;
        trial.UpdatedBy = employeeId;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
