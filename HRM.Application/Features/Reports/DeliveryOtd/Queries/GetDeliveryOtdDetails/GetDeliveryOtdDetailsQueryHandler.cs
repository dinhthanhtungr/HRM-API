using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdDetails;

internal sealed class GetDeliveryOtdDetailsQueryHandler(IDispatchReadDbContext db, ICurrentUser user, IDateTimeProvider clock)
    : IRequestHandler<GetDeliveryOtdDetailsQuery, OperationResult<DeliveryOtdDetailsDto>>
{
    public async Task<OperationResult<DeliveryOtdDetailsDto>> Handle(GetDeliveryOtdDetailsQuery request, CancellationToken ct)
    {
        var today = clock.Now.Date;
        if (!DeliveryOtdReader.IsValid(request, user, today) || request.Page < 1 || request.PageSize is < 1 or > 200
            || (long)(request.Page - 1) * request.PageSize > int.MaxValue
            || (request.Status.HasValue && !Enum.IsDefined(request.Status.Value)))
            return OperationResult<DeliveryOtdDetailsDto>.Fail("Không có quyền hoặc bộ lọc OTD không hợp lệ.");
        var asOf = request.AsOf?.Date ?? today;
        var rows = await DeliveryOtdReader.ReadAsync(db, request, user.CompanyId!.Value, asOf, ct);
        var filtered = rows.Where(x => !request.Status.HasValue || x.Status == request.Status.Value)
            .OrderBy(x => x.DueDate).ThenBy(x => x.DetailId).ToList();
        var result = new DeliveryOtdDetailsDto(filtered.Count, request.Page, request.PageSize,
            filtered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList());
        return OperationResult<DeliveryOtdDetailsDto>.Ok(result);
    }
}
