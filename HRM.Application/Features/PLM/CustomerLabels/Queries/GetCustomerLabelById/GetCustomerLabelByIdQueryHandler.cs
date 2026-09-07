using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using HRM.Application.Features.PLM.CustomerLabels.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.CustomerLabels.Queries.GetCustomerLabelById;

internal sealed class GetCustomerLabelByIdQueryHandler : IRequestHandler<GetCustomerLabelByIdQuery, CustomerLabelDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetCustomerLabelByIdQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public Task<CustomerLabelDto?> Handle(GetCustomerLabelByIdQuery request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (request.CustomerLabelHeaderId == Guid.Empty || companyId == Guid.Empty)
        {
            return Task.FromResult<CustomerLabelDto?>(null);
        }

        return _dbContext.CustomerLabelHeaders.AsNoTracking()
            .Where(header => header.Id == request.CustomerLabelHeaderId &&
                header.Product.CompanyId == companyId && header.Product.IsActive &&
                header.Customer.CompanyId == companyId && header.Customer.IsActive == true)
            .Select(CustomerLabelProjection.ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
