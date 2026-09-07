using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;

internal sealed class GetSampleRequestLookupQueryHandler
    : IRequestHandler<GetSampleRequestLookupQuery, IReadOnlyList<SampleRequestLookupItemDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetSampleRequestLookupQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SampleRequestLookupItemDto>> Handle(
        GetSampleRequestLookupQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");

        var query = _dbContext.SampleRequests
            .Where(x =>
                x.CompanyId == companyId &&
                (x.Product.ColourCode != null || x.Product.Name != null))
            .AsNoTracking()
            .AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (request.CompanyId is { } requestedCompanyId && requestedCompanyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == requestedCompanyId);
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            if (!request.ForSaleOrder)
            {
                query = query.Where(x =>
                    x.CustomerId == customerId ||
                    x.Customer.ExternalId == InternalCustomerRules.InternalCustomerExternalId);
            }
            else
            {
                var saleOrderCustomer = await _dbContext.Customers
                    .AsNoTracking()
                    .Where(x =>
                        (x.CustomerId == customerId ) &&
                        x.CompanyId == companyId &&
                        x.IsActive == true)
                    .Select(x => x.ExternalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (saleOrderCustomer is null)
                {
                    return Array.Empty<SampleRequestLookupItemDto>();
                }

                if (!InternalCustomerRules.IsInternalCustomerExternalId(saleOrderCustomer))
                {
                    query = query.Where(x =>
                        x.CustomerId == customerId ||
                        x.Customer.ExternalId == InternalCustomerRules.InternalCustomerExternalId);
                }
            }
        }
        else if (request.ForSaleOrder)
        {
            return Array.Empty<SampleRequestLookupItemDto>();
        }

        if (request.SampleRequestId is { } sampleRequestId && sampleRequestId != Guid.Empty)
        {
            query = query.Where(x => x.SampleRequestId == sampleRequestId);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedStatus))
        {
            var status = request.NormalizedStatus;
            query = query.Where(x => x.Status == status);
        }

        if (request.ForSaleOrder)
        {
            var sampleSent = SampleRequestStatus.SampleSent.ToString();
            var completed = SampleRequestStatus.Completed.ToString();
            query = query.Where(x => x.Status == sampleSent || x.Status == completed);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;

            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Customer.ExternalId.Contains(keyword) ||
                x.Customer.CustomerName.Contains(keyword) ||
                (x.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                (x.Product.Name ?? string.Empty).Contains(keyword) ||
                (x.Product.Code ?? string.Empty).Contains(keyword) ||
                (x.Formula != null && EF.Functions.ILike(x.Formula.ExternalId, $"%{keyword}%")));

        }

        return await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.SampleRequestId)
            .Take(request.NormalizedTake)
            .Select(x => new SampleRequestLookupItemDto
            {
                SampleRequestId = x.SampleRequestId,
                ExternalId = x.ExternalId,
                Status = x.Status,
                CustomerId = x.CustomerId,
                CustomerExternalId = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                ProductId = x.ProductId,
                ProductCode = x.Product.ColourCode,
                ProductName = x.Product.Name,
                FormulaId = x.FormulaId.HasValue && x.Formula!.IsActive
                    ? x.FormulaId
                    : x.SampleRequestSampleTrials
                        .Where(trial => trial.IsActive && trial.FormulaId.HasValue && trial.Formula!.IsActive)
                        .OrderByDescending(trial => trial.TrialNo)
                        .ThenByDescending(trial => trial.CreatedDate)
                        .Select(trial => trial.FormulaId)
                        .FirstOrDefault(),
                FormulaExternalId = x.FormulaId.HasValue && x.Formula!.IsActive
                    ? x.Formula!.ExternalId
                    : x.SampleRequestSampleTrials
                        .Where(trial => trial.IsActive && trial.FormulaId.HasValue && trial.Formula!.IsActive)
                        .OrderByDescending(trial => trial.TrialNo)
                        .ThenByDescending(trial => trial.CreatedDate)
                        .Select(trial => trial.Formula!.ExternalId)
                        .FirstOrDefault(),
                CreatedDate = x.CreatedDate,
                Label = x.ExternalId + " - " + x.Customer.CustomerName + " - " + (x.Product.Name ?? x.Product.ColourCode ?? x.Product.Code ?? string.Empty)
            })
            .ToListAsync(cancellationToken);
    }
}
