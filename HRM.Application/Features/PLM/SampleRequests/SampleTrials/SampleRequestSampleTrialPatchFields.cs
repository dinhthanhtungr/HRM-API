namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

public static class SampleRequestSampleTrialPatchFields
{
    public const string FormulaId = "formulaId";
    public const string FormulaExternalId = "formulaExternalId";
    public const string BatchNo = "batchNo";
    public const string DeliveredSampleQuantityKg = "deliveredSampleQuantityKg";
    public const string AdditiveRate = "additiveRate";
    public const string RequestDeliveryDate = "requestDeliveryDate";
    public const string ExpectedDeliveryDate = "expectedDeliveryDate";
    public const string RequestReceivedDate = "requestReceivedDate";
    public const string FinishedDate = "finishedDate";
    public const string SentDate = "sentDate";
    public const string DeliveryMethod = "deliveryMethod";
    public const string LabNote = "labNote";
    public const string SentByEmployeeId = "sentByEmployeeId";
    public const string Status = "status";
    public const string CustomerReplyStatus = "customerReplyStatus";
    public const string CustomerReplyNote = "customerReplyNote";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        FormulaId,
        BatchNo,
        DeliveredSampleQuantityKg,
        AdditiveRate,
        RequestDeliveryDate,
        ExpectedDeliveryDate,
        RequestReceivedDate,
        FinishedDate,
        SentDate,
        DeliveryMethod,
        LabNote,
        SentByEmployeeId,
        CustomerReplyStatus,
        CustomerReplyNote
    };
}
