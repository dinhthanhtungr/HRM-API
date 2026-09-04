using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Application.Features.PLM.ColorChipRecords.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ColorChipRecords.Queries.GetColorChipRecordById;

internal sealed class GetColorChipRecordByIdQueryHandler
    : IRequestHandler<GetColorChipRecordByIdQuery, ColorChipRecordDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetColorChipRecordByIdQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public Task<ColorChipRecordDto?> Handle(
        GetColorChipRecordByIdQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (request.ColorChipRecordId == Guid.Empty || companyId == Guid.Empty)
        {
            return Task.FromResult<ColorChipRecordDto?>(null);
        }

        return _dbContext.ColorChipRecords
            .AsNoTracking()
            .Where(x =>
                x.ColorChipRecordId == request.ColorChipRecordId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Product != null &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive)
            .Select(ColorChipRecordProjection.ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
