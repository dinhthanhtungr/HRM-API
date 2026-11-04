using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdHeader;

internal sealed class GetDeliveryOtdHeaderQueryHandler(IDispatchReadDbContext db, ICurrentUser user, IDateTimeProvider clock)
    : IRequestHandler<GetDeliveryOtdHeaderQuery, OperationResult<DeliveryOtdHeaderDto>>
{
    public async Task<OperationResult<DeliveryOtdHeaderDto>> Handle(GetDeliveryOtdHeaderQuery request, CancellationToken ct)
    {
        var today = clock.Now.Date;
        if (!DeliveryOtdReader.IsValid(request, user, today))
            return OperationResult<DeliveryOtdHeaderDto>.Fail("Không có quyền hoặc bộ lọc OTD không hợp lệ.");
        var asOf = request.AsOf?.Date ?? today;
        var rows = await DeliveryOtdReader.ReadAsync(db, request, user.CompanyId!.Value, asOf, ct);
        var result = new DeliveryOtdHeaderDto(request.From.Date, request.To.Date, asOf, request.DateBasis,
            DeliveryOtdCalculator.Summarize(rows, asOf))
        {
            PoSummary = await DeliveryOtdReader.ReadPoSummaryAsync(db, rows, user.CompanyId.Value, asOf, ct),
            DeliveredQuantityInPeriod = await DeliveryOtdPeriodDeliveryReader.ReadAsync(db, request, user.CompanyId.Value, asOf, ct)
        };
        return OperationResult<DeliveryOtdHeaderDto>.Ok(result);
    }
}
