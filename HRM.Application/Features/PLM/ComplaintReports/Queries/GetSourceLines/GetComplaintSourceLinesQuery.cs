using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetSourceLines;

public sealed record GetComplaintSourceLinesQuery(
    Guid CustomerId,
    Guid? MerchandiseOrderId,
    string? Keyword,
    int Take = 50) : IRequest<IReadOnlyList<ComplaintSourceLineDto>>
{
    public string? NormalizedKeyword => string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
    public int NormalizedTake => Take <= 0 ? 50 : Math.Min(Take, 100);
}
