using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Rules;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetSupplierDetail;

internal sealed class GetSupplierDetailQueryHandler : IRequestHandler<GetSupplierDetailQuery, SupplierDetailDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetSupplierDetailQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<SupplierDetailDto?> Handle(GetSupplierDetailQuery request, CancellationToken cancellationToken)
    {
        if (request.SupplierId == Guid.Empty || _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty) return null;

        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .Where(x => x.SupplierId == request.SupplierId && x.CompanyId == companyId && x.IsActive == true)
            .Select(x => new SupplierDetailDto
            {
                SupplierId = x.SupplierId,
                SupplierCode = x.ExternalId ?? string.Empty,
                SupplierName = x.SupplierName ?? string.Empty,
                RegistrationNumber = x.RegistrationNumber,
                RegistrationAddress = x.RegistrationAddress,
                TaxNumber = x.TaxNumber,
                Phone = x.Phone,
                Website = x.Website,
                Note = x.Note,
                IssueDate = x.IssueDate,
                IssuedPlace = x.IssuedPlace,
                FaxNumber = x.FaxNumber,
                LatestOrderDate = x.PurchaseOrders
                    .Where(o => o.CompanyId == companyId && o.IsActive == true &&
                                (o.Status == null || !PurchaseOrderPriceRules.CanceledStatuses.Contains(o.Status)))
                    .Max(o => o.CreateDate),
                Contacts = x.SupplierContacts.Where(c => c.IsActive == true)
                    .OrderByDescending(c => c.IsPrimary == true).ThenBy(c => c.ContactId)
                    .Select(c => new SupplierContactDto
                    {
                        ContactId = c.ContactId, FirstName = c.FirstName, LastName = c.LastName,
                        Phone = c.Phone, Email = c.Email, IsPrimary = c.IsPrimary == true
                    }).ToList(),
                Addresses = x.SupplierAddresses.Where(a => a.IsActive == true)
                    .OrderByDescending(a => a.IsPrimary == true).ThenBy(a => a.AddressId)
                    .Select(a => new SupplierAddressDto
                    {
                        AddressId = a.AddressId, AddressLine = a.AddressLine, City = a.City,
                        District = a.District, Province = a.Province, Country = a.Country,
                        IsPrimary = a.IsPrimary == true, PostalCode = a.PostalCode
                    }).ToList()
            }).FirstOrDefaultAsync(cancellationToken);

        if (supplier is null) return null;

        var materialsQuery = _dbContext.MaterialsSuppliers.AsNoTracking()
            .Where(x => x.SupplierId == request.SupplierId && x.IsActive == true &&
                        x.Material.CompanyId == companyId && x.Material.IsActive == true);
        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(request.NormalizedKeyword);
            materialsQuery = materialsQuery.Where(x =>
                EF.Functions.ILike(x.Material.ExternalId ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Material.Name ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var total = await materialsQuery.CountAsync(cancellationToken);
        var materials = await materialsQuery.OrderBy(x => x.Material.ExternalId).ThenBy(x => x.Material.Name).ThenBy(x => x.MaterialId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize)
            .Select(x => new SupplierMaterialDto
            {
                MaterialId = x.MaterialId, MaterialCode = x.Material.ExternalId ?? string.Empty,
                MaterialName = x.Material.Name ?? string.Empty, Unit = x.Material.Unit,
                CurrentPrice = x.CurrentPrice, Currency = x.Currency,
                IsPreferred = x.IsPreferred == true, MinDeliveryDays = x.MinDeliveryDays
            }).ToListAsync(cancellationToken);

        supplier.Materials = new PagedResult<SupplierMaterialDto>(materials, total, request.NormalizedPageNumber, request.NormalizedPageSize);
        return supplier;
    }
}
