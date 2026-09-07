using HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;

public sealed class GetSampleRequestLookupQuery : IRequest<IReadOnlyList<SampleRequestLookupItemDto>>
{
    public Guid? CompanyId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? SampleRequestId { get; init; }
    public string? Keyword { get; init; }
    public string? Status { get; init; }
    /// <summary>
    /// Chỉ dùng cho dropdown lập SaleOrder; giới hạn Sample Request ở SampleSent hoặc Completed.
    /// Khi truyền CustomerId của khách ngoài, lookup trả TP thuộc khách đó và KH_VIETAUS.
    /// Khi CustomerId là KH_VIETAUS, lookup trả các Sample Request hợp lệ của mọi khách trong công ty hiện tại.
    /// </summary>
    public bool ForSaleOrder { get; init; }
    public bool? IsActive { get; init; } = true;
    public int Take { get; init; } = 20;

    public string? NormalizedKeyword => string.IsNullOrWhiteSpace(Keyword)
        ? null
        : Keyword.Trim();

    public string? NormalizedStatus => string.IsNullOrWhiteSpace(Status)
        ? null
        : Status.Trim();

    public int NormalizedTake => Take <= 0 ? 20 : Math.Min(Take, 50);
}
