using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdHeader;

internal sealed class GetDeliveryOtdHeaderQueryHandler(IDispatchReadDbContext db, ICurrentUser user, IDateTimeProvider clock,
    ICurrentUserPermissionService? permissions = null)
    : IRequestHandler<GetDeliveryOtdHeaderQuery, OperationResult<DeliveryOtdHeaderDto>>
{
    public async Task<OperationResult<DeliveryOtdHeaderDto>> Handle(GetDeliveryOtdHeaderQuery request, CancellationToken ct)
    {
        var today = clock.Now.Date;
        if (!DeliveryOtdReader.IsValid(request, user, today, permissions))
            return OperationResult<DeliveryOtdHeaderDto>.Fail("Không có quyền hoặc bộ lọc OTD không hợp lệ.");
        var asOf = request.AsOf?.Date ?? today;
        var restricted = ImsAccountRestrictions.AppliesTo(user);
        var rows = await DeliveryOtdReader.ReadAsync(db, request, user.CompanyId!.Value, asOf, ct, restricted);
        var result = new DeliveryOtdHeaderDto(request.From.Date, request.To.Date, asOf, request.DateBasis,
            DeliveryOtdCalculator.Summarize(rows, asOf))
        {
            PoSummary = await DeliveryOtdReader.ReadPoSummaryAsync(db, rows, user.CompanyId.Value, asOf, ct, restricted),
            DeliveredQuantityInPeriod = await DeliveryOtdPeriodDeliveryReader.ReadAsync(db, request, user.CompanyId.Value, asOf, ct, restricted)
        };
        return OperationResult<DeliveryOtdHeaderDto>.Ok(result);
    }
}
