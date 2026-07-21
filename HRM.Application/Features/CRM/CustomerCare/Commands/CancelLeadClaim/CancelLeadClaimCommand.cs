using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CancelLeadClaim;

/// <summary>
/// Feature CRM CustomerCare - hủy một người đang chăm sóc lead bằng cách soft-disable CustomerClaim Work.
/// Command không xóa cứng claim để giữ lịch sử nghiệp vụ và audit gián tiếp qua log dữ liệu.
/// </summary>
public sealed record CancelLeadClaimCommand(Guid CustomerId, Guid ClaimId)
    : IRequest<OperationResult>;
