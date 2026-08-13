namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

public static class SampleRequestSampleTrialPatchFields
{
    public const string FormulaId = "formulaId";
    public const string BatchNo = "batchNo";
    public const string DeliveredSampleQuantityKg = "deliveredSampleQuantityKg";
    public const string AdditiveRate = "additiveRate";
    public const string RequestReceivedDate = "requestReceivedDate";
    public const string FinishedDate = "finishedDate";
    public const string SentDate = "sentDate";
    public const string DeliveryMethod = "deliveryMethod";
    public const string LabNote = "labNote";
    public const string SentByEmployeeId = "sentByEmployeeId";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        FormulaId,
        BatchNo,
        DeliveredSampleQuantityKg,
        AdditiveRate,
        RequestReceivedDate,
        FinishedDate,
        SentDate,
        DeliveryMethod,
        LabNote,
        SentByEmployeeId
    };
}
