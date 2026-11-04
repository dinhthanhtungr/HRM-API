using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

/// <summary>Read model từ sự kiện nghiệp vụ, không phải cột của Sample Request.</summary>
public sealed record CustomerPriceConfirmationDto(
    bool IsActive, DateTime RecordedAt, Guid? RecordedByEmployeeId, string? RecordedByName,
    DateTime? CustomerAcceptedAt, Guid? QuotationLineId, string? PriceReference, string? Note, Guid? WorkflowEventId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool IsContentHidden = false);
