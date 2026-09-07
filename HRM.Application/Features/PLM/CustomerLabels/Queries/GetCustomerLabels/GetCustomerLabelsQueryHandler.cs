using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using HRM.Application.Features.PLM.CustomerLabels.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.CustomerLabels.Queries.GetCustomerLabels;

internal sealed class GetCustomerLabelsQueryHandler : IRequestHandler<GetCustomerLabelsQuery, IReadOnlyList<CustomerLabelDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetCustomerLabelsQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CustomerLabelDto>> Handle(GetCustomerLabelsQuery request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (companyId == Guid.Empty || request.ProductId == Guid.Empty || request.CustomerId == Guid.Empty)
        {
            return Array.Empty<CustomerLabelDto>();
        }

        var query = _dbContext.CustomerLabelHeaders.AsNoTracking()
            .Where(header => header.Product.CompanyId == companyId && header.Product.IsActive &&
                header.Customer.CompanyId == companyId && header.Customer.IsActive == true);

        if (request.ProductId.HasValue)
        {
            query = query.Where(header => header.ProductId == request.ProductId.Value);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(header => header.CustomerId == request.CustomerId.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(header => header.IsActive == request.IsActive.Value);
        }

        return await query.OrderByDescending(header => header.UpdatedDate ?? header.CreatedDate)
            .Select(CustomerLabelProjection.ToDto)
            .ToListAsync(cancellationToken);
    }
}
