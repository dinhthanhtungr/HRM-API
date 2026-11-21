using MediatR;
namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
public sealed record SampleDeliveryCandidate(Guid FormulaId, string Code, DateTime? UpdatedDate)
{
    public string? Name { get; init; }
    public string? Note { get; init; }
    public string? Status { get; init; }
    public bool Confirmed { get; init; } = true;
}
public sealed record SampleDeliveryOption(Guid TrialId, int OptionNo, Guid? FormulaId, string? FormulaCode,
    decimal? QuantityKg, string Status, DateTime? ReplyDate, string? ReplyNote, DateTime? UpdatedDate)
{
    public DateTime? FormulaUpdatedDate { get; init; }
    public bool CanComplete { get; init; }
}
public sealed record SampleDeliveryBatch(Guid BatchId, DateTime SentDate, bool CanRecord, bool CanCorrect,
    IReadOnlyList<SampleDeliveryOption> Options);
public sealed record SampleDeliveriesDto(DateTime? RequestUpdatedDate, IReadOnlyList<SampleDeliveryCandidate> Candidates,
    IReadOnlyList<SampleDeliveryBatch> Batches);
public sealed record SendSampleOption(Guid FormulaId, decimal QuantityKg, DateTime ExpectedUpdatedDate)
{
    // Omitted/null keeps the formula note as the delivery snapshot; empty explicitly clears it.
    public string? Note { get; init; }
}
public sealed record GetSampleDeliveriesQuery(Guid SampleRequestId, bool ForSending = false) : IRequest<SampleDeliveriesDto?>;
