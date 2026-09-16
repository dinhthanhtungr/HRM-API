using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialPriceReviewSuppliers;

internal sealed class GetMaterialPriceReviewSuppliersQueryHandler
    : IRequestHandler<GetMaterialPriceReviewSuppliersQuery, MaterialPriceReviewSuppliersDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetMaterialPriceReviewSuppliersQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<MaterialPriceReviewSuppliersDto?> Handle(
        GetMaterialPriceReviewSuppliersQuery request,
        CancellationToken cancellationToken)
    {
        if (request.MaterialId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        var materialExists = await _dbContext.Materials
            .AsNoTracking()
            .AnyAsync(x =>
                x.MaterialId == request.MaterialId &&
                x.CompanyId == companyId &&
                x.IsActive == true,
                cancellationToken);

        if (!materialExists)
        {
            return null;
        }

        var suppliers = await _dbContext.MaterialsSuppliers
            .AsNoTracking()
            .Where(x =>
                x.MaterialId == request.MaterialId &&
                x.IsActive == true &&
                x.Supplier.IsActive == true &&
                x.Supplier.CompanyId == companyId)
            .OrderByDescending(x => x.IsPreferred == true)
            .ThenBy(x => x.Supplier.SupplierName)
            .Select(x => new MaterialPriceReviewSupplierDto
            {
                MaterialsSupplierId = x.MaterialsSuppliersId,
                SupplierId = x.SupplierId,
                SupplierCode = x.Supplier.ExternalId ?? string.Empty,
                SupplierName = x.Supplier.SupplierName ?? string.Empty,
                CurrentPrice = x.CurrentPrice,
                Currency = x.Currency,
                IsPreferred = x.IsPreferred == true,
                LastPriceUpdatedAt = x.UpdatedDate ?? x.CreateDate
            })
            .ToListAsync(cancellationToken);

        return new MaterialPriceReviewSuppliersDto
        {
            MaterialId = request.MaterialId,
            Suppliers = suppliers
        };
    }
}
