using HRM.Application.Features.CRM.CustomerCare.Dtos;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleTrialCustomerFeedbackInteraction;

internal static class SampleTrialCustomerFeedbackInteractionMapper
{
    public static CreateSampleTrialInteractionRequest ToCrmRequest(
        RecordSampleTrialCustomerFeedbackInteractionCommand command,
        Guid customerId)
        => new()
        {
            IdempotencyKey = command.IdempotencyKey,
            CustomerId = customerId,
            SampleRequestSampleTrialId = command.SampleRequestSampleTrialId,
            ContactId = command.ContactId,
            InteractionType = command.InteractionType,
            Subject = command.Subject,
            Content = command.Content,
            Outcome = command.Outcome,
            NextAction = command.NextAction,
            InteractionAt = command.InteractionAt,
            NextFollowUpDate = command.NextFollowUpDate,
            AssignedSaleEmployeeId = command.AssignedSaleEmployeeId,
            CustomerReplyStatus = command.CustomerReplyStatus,
            CustomerReplyDate = command.CustomerReplyDate,
            CustomerReplyNote = command.CustomerReplyNote,
            OrderDate = command.OrderDate,
            ExpectedTrialUpdatedDate = command.ExpectedTrialUpdatedDate
        };
}
