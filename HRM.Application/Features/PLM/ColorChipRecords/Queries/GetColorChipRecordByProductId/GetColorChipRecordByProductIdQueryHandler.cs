using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Application.Features.PLM.ColorChipRecords.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ColorChipRecords.Queries.GetColorChipRecordByProductId;

internal sealed class GetColorChipRecordByProductIdQueryHandler
    : IRequestHandler<GetColorChipRecordByProductIdQuery, ColorChipRecordDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetColorChipRecordByProductIdQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public Task<ColorChipRecordDto?> Handle(
        GetColorChipRecordByProductIdQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (request.ProductId == Guid.Empty || companyId == Guid.Empty)
        {
            return Task.FromResult<ColorChipRecordDto?>(null);
        }

        return _dbContext.ColorChipRecords
            .AsNoTracking()
            .Where(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Product != null &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive)
            .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(ColorChipRecordProjection.ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
