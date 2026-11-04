using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.ReviewStandardSamples;

internal sealed class ReviewStandardSamplesQueryHandler(SaleOrderStandardSampleService samples,
    ICurrentUser user, IDateTimeProvider clock)
    : IRequestHandler<ReviewStandardSamplesQuery, OperationResult<IReadOnlyList<StandardSampleReviewDto>>>
{
    public Task<OperationResult<IReadOnlyList<StandardSampleReviewDto>>> Handle(ReviewStandardSamplesQuery request, CancellationToken ct)
    {
        SaleOrderCurrentUserGuard.RequireEmployeeId(user);
        return samples.ReviewAsync(request.Request, SaleOrderCurrentUserGuard.RequireCompanyId(user), clock.Now, ct);
    }
}
