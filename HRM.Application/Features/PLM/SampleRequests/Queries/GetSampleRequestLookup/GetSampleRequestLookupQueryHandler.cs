using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;

internal sealed class GetSampleRequestLookupQueryHandler
    : IRequestHandler<GetSampleRequestLookupQuery, IReadOnlyList<SampleRequestLookupItemDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetSampleRequestLookupQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
    }

    public async Task<IReadOnlyList<SampleRequestLookupItemDto>> Handle(
        GetSampleRequestLookupQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        string? saleOrderCustomerExternalId = null;
        var canUseAllCustomersFormula = false;

        if (request.ForSaleOrder)
        {
            if (request.CustomerId is not { } saleOrderCustomerId || saleOrderCustomerId == Guid.Empty)
            {
                return Array.Empty<SampleRequestLookupItemDto>();
            }

            saleOrderCustomerExternalId = await _dbContext.Customers
                .AsNoTracking()
                .Where(customer =>
                    customer.CustomerId == saleOrderCustomerId &&
                    customer.CompanyId == companyId &&
                    customer.IsActive == true)
                .Select(customer => customer.ExternalId)
                .FirstOrDefaultAsync(cancellationToken);

            if (saleOrderCustomerExternalId is null)
            {
                return Array.Empty<SampleRequestLookupItemDto>();
            }

            canUseAllCustomersFormula = PLMCustomerRules.CanUseAllCustomersFormula(
                request.OrderType,
                PLMCustomerRules.IsInternalCustomerExternalId(saleOrderCustomerExternalId));
        }

        // KH_VIETAUS chỉ tham gia kết quả khi Sale chủ động tìm keyword,
        // đồng nhất với endpoint Sample Request Summary.
        var visibilityScope = canUseAllCustomersFormula
            ? scope with { HasFullCustomerView = true, CanViewInternalCustomer = true }
            : request.ForSaleOrder && request.NormalizedKeyword is not null
                ? scope with { CanViewInternalCustomer = true }
                : scope;

        var query = _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests
                    .AsNoTracking()
                    .Where(x =>
                        x.Product.IsActive &&
                        x.Product.CompanyId == companyId &&
                        x.Customer.IsActive == true &&
                        x.Customer.CompanyId == companyId &&
                        (x.Product.ColourCode != null || x.Product.Name != null)),
                _dbContext.Customers.AsNoTracking(),
                visibilityScope)
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
                if (!canUseAllCustomersFormula)
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
            var allowsSampleSent = PLMCustomerRules.AllowsSampleSentFormula(
                request.OrderType,
                PLMCustomerRules.IsInternalCustomerExternalId(saleOrderCustomerExternalId));
            query = allowsSampleSent
                ? query.Where(x => x.Status == sampleSent || x.Status == completed)
                : query.Where(x => x.Status == completed);
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
