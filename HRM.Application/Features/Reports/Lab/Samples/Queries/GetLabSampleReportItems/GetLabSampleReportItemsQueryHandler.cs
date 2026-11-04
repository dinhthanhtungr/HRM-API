using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Lab.Samples.Queries.GetLabSampleReportItems;

internal sealed class GetLabSampleReportItemsQueryHandler(IPLMReadDbContext db, ICustomerVisibilityService visibility, ICurrentUser user)
    : IRequestHandler<GetLabSampleReportItemsQuery, OperationResult<PagedResult<LabSampleRowDto>>>
{
    public async Task<OperationResult<PagedResult<LabSampleRowDto>>> Handle(GetLabSampleReportItemsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is null || user.CompanyId == Guid.Empty
            || user.EmployeeId is null || user.EmployeeId == Guid.Empty
            || request.CompanyId.HasValue && request.CompanyId != user.CompanyId)
            return OperationResult<PagedResult<LabSampleRowDto>>.Fail("Invalid current user/company scope.");
        var error = LabSampleReportRules.Validate(request);
        if (error != null) return OperationResult<PagedResult<LabSampleRowDto>>.Fail(error);
        if (!Enum.IsDefined(request.Metric) || request.PageNumber < 1 || request.PageSize is < 1 or > 100
            || (long)(request.PageNumber - 1) * request.PageSize > int.MaxValue)
            return OperationResult<PagedResult<LabSampleRowDto>>.Fail("Invalid metric or pagination.");
        var query = (await new LabSampleReportReader(db, visibility).QueryAsync(request, ct))
            .Where(LabSampleReportRules.Predicate(request.Metric));
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.SampleRequestId)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        foreach (var item in items)
        {
            item.DevelopmentLeadTimeDays = LabSampleReportRules.LeadTime(item.ColourAssignedAt, item.LabApprovedAt);
            item.ApprovedForPo = LabSampleReportRules.Conversion(item);
        }
        return OperationResult<PagedResult<LabSampleRowDto>>.Ok(new PagedResult<LabSampleRowDto>(items, count, request.PageNumber, request.PageSize));
    }
}
