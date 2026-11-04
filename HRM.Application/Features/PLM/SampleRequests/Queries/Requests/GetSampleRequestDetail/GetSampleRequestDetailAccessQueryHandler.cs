using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;

/// <summary>
/// Chỉ dùng khi query detail không có kết quả để trả HTTP error phù hợp.
/// Lookup Sale Order dùng cùng điều kiện active/visibility nên bình thường
/// không thể dẫn đến các trạng thái này sau khi user đã chọn một item.
/// </summary>
internal sealed class GetSampleRequestDetailAccessQueryHandler
    : IRequestHandler<GetSampleRequestDetailAccessQuery, SampleRequestDetailAccessStatus>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ICurrentUser? _currentUser;

    public GetSampleRequestDetailAccessQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        ICurrentUser? currentUser = null)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _currentUser = currentUser;
    }

    public async Task<SampleRequestDetailAccessStatus> Handle(
        GetSampleRequestDetailAccessQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return SampleRequestDetailAccessStatus.NotFound;
        }

        var scope = await _visibilityService.BuildReadScopeAsync(cancellationToken);
        var detailScope = _currentUser is null ? scope : await HRM.Application.Features.PLM.SampleRequests.Services.SampleWorkPlanningVisibility.ForPlanningAsync(
            scope, _currentUser, _dbContext.Employees, cancellationToken);
        var activeRecordInCurrentCompany = _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.SampleRequestId == request.SampleRequestId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive);

        if (!await activeRecordInCurrentCompany.AnyAsync(cancellationToken))
        {
            return SampleRequestDetailAccessStatus.NotFound;
        }

        var validRecord = activeRecordInCurrentCompany.Where(x =>
            x.Product.IsActive &&
            x.Product.CompanyId == scope.CompanyId &&
            x.Customer.IsActive == true &&
            x.Customer.CompanyId == scope.CompanyId);
        if (!scope.HasFullCustomerView && detailScope.HasFullCustomerView)
            validRecord = validRecord.Where(x => x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
                x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);

        if (!await validRecord.AnyAsync(cancellationToken))
        {
            return SampleRequestDetailAccessStatus.InvalidRelationship;
        }

        var isVisible = await _visibilityService.ApplySampleRequestVisibility(
                validRecord,
                _dbContext.Customers.AsNoTracking(),
                detailScope)
            .AnyAsync(cancellationToken);

        return isVisible
            ? SampleRequestDetailAccessStatus.NotFound
            : SampleRequestDetailAccessStatus.Forbidden;
    }
}
