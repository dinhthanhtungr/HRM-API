using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;
using HRM.Application.Features.Purchasing.MaterialPlanning.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningOrders;

internal sealed class GetPlanningOrdersQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPlanningOrdersQuery, OperationResult<PagedResult<PlanningOrderDto>>>
{
    public async Task<OperationResult<PagedResult<PlanningOrderDto>>> Handle(GetPlanningOrdersQuery request, CancellationToken ct)
    {
        if (!MaterialPlanningScope.CanRead(user, permissions))
            return OperationResult<PagedResult<PlanningOrderDto>>.Fail("Không có quyền hoặc thiếu công ty hiện tại.");
        var query = MaterialPlanningScope.Orders(db, user.CompanyId!.Value)
            .WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.PONo, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.CustomerNameSnapshot, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
        var count = await query.CountAsync(ct);
        var skip = (int)Math.Min((long)(request.NormalizedPageNumber - 1) * request.NormalizedPageSize, int.MaxValue);
        var rows = await query.OrderByDescending(x => x.ExternalId).ThenBy(x => x.MerchandiseOrderId)
            .Skip(skip).Take(request.NormalizedPageSize)
            .Select(x => new PlanningOrderDto(x.MerchandiseOrderId, x.ExternalId, x.PONo,
                x.CustomerNameSnapshot, x.Status, x.MerchandiseOrderDetails.Count(d => d.IsActive)))
            .ToListAsync(ct);
        return OperationResult<PagedResult<PlanningOrderDto>>.Ok(new PagedResult<PlanningOrderDto>(rows, count,
            request.NormalizedPageNumber, request.NormalizedPageSize));
    }
}
