using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetLeadClaims;

/// <summary>
/// Feature CRM CustomerCare - lấy danh sách người đang claim lead và các interaction gần nhất của từng người.
/// Query dùng cho màn detail lead, không trả toàn bộ hồ sơ customer.
/// </summary>
public sealed record GetLeadClaimsQuery(Guid CustomerId, int InteractionLimit = 10)
    : IRequest<OperationResult<LeadClaimDetailsDto>>
{
    public int NormalizedInteractionLimit => InteractionLimit switch
    {
        <= 0 => 10,
        > 100 => 100,
        _ => InteractionLimit
    };
}
