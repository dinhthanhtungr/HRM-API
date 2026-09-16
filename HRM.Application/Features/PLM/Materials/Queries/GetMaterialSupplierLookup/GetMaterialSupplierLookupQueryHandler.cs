using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialSupplierLookup;

internal sealed class GetMaterialSupplierLookupQueryHandler
    : IRequestHandler<GetMaterialSupplierLookupQuery, IReadOnlyList<MaterialSupplierLookupDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetMaterialSupplierLookupQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MaterialSupplierLookupDto>> Handle(
        GetMaterialSupplierLookupQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return [];

        var suppliers = _dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive == true);

        if (request.MaterialId is { } materialId)
        {
            if (materialId == Guid.Empty || !await _dbContext.Materials
                    .AsNoTracking()
                    .AnyAsync(x => x.MaterialId == materialId && x.CompanyId == companyId && x.IsActive == true,
                        cancellationToken))
            {
                return [];
            }

            suppliers = suppliers.Where(supplier => !_dbContext.MaterialsSuppliers.Any(link =>
                link.MaterialId == materialId &&
                link.SupplierId == supplier.SupplierId &&
                link.IsActive == true));
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(request.Keyword.Trim());
            suppliers = suppliers.Where(x =>
                EF.Functions.ILike(x.ExternalId ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.SupplierName ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        return await suppliers
            .OrderBy(x => x.SupplierName)
            .ThenBy(x => x.ExternalId)
            .ThenBy(x => x.SupplierId)
            .Take(request.NormalizedPageSize)
            .Select(x => new MaterialSupplierLookupDto
            {
                SupplierId = x.SupplierId,
                SupplierCode = x.ExternalId ?? string.Empty,
                SupplierName = x.SupplierName ?? string.Empty
            })
            .ToListAsync(cancellationToken);
    }
}
