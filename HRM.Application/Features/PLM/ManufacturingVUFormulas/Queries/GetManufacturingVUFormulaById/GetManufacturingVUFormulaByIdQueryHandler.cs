using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulaById;

internal sealed class GetManufacturingVUFormulaByIdQueryHandler
    : IRequestHandler<
        GetManufacturingVUFormulaByIdQuery,
        OperationResult<ManufacturingVUFormulaDetailDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPLMFieldVisibilityService _fieldVisibility;

    public GetManufacturingVUFormulaByIdQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IPLMFieldVisibilityService fieldVisibility)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fieldVisibility = fieldVisibility;
    }

    public async Task<OperationResult<ManufacturingVUFormulaDetailDto>> Handle(
        GetManufacturingVUFormulaByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ManufacturingVUFormulaId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaDetailDto>
                .Fail("ManufacturingVUFormulaId is invalid.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaDetailDto>
                .Fail("Current company is invalid.");
        }

        var detail = await _dbContext.ManufacturingVUFormulas
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingVUFormulaId == request.ManufacturingVUFormulaId &&
                x.Formula.Product.CompanyId == companyId)
            .Select(x => new ManufacturingVUFormulaDetailDto
            {
                ManufacturingVUFormulaId = x.ManufacturingVUFormulaId,
                FormulaId = x.FormulaId,
                FormulaExternalId = x.Formula.ExternalId,
                FormulaName = x.Formula.Name,
                ProductId = x.Formula.ProductId,
                ProductName = x.Formula.Product.Name,
                ColourCode = x.Formula.Product.ColourCode,
                UsageRate = x.Formula.Product.UsageRate,
                TotalProductionQuantity = x.TotalProductionQuantity,
                NumOfBatches = x.NumOfBatches,
                Status = x.status,
                LabNote = x.LabNote,
                Requirement = x.Requirement,
                QcCheck = x.QcCheck,
                CreatedDate = x.CreatedDate,
                CreatedByName = x.CreatedByNavigation != null
                    ? x.CreatedByNavigation.FullName
                    : null,
                UpdatedDate = x.UpdatedDate,
                UpdatedByName = x.UpdatedByNavigation != null
                    ? x.UpdatedByNavigation.FullName
                    : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return OperationResult<ManufacturingVUFormulaDetailDto>
                .Fail("Sample production order was not found or is outside your company.");
        }

        var customer = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                x.ProductId == detail.ProductId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                Code = x.Customer.ExternalId,
                Name = x.Customer.CustomerName
            })
            .FirstOrDefaultAsync(cancellationToken);

        detail.CustomerCode = customer?.Code;
        detail.CustomerName = customer?.Name;

        var canViewPrices = _fieldVisibility.CanViewFormulaPrices();
        var materialRows = await _dbContext.FormulaMaterialSnapshots
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingVUFormulaId == request.ManufacturingVUFormulaId &&
                x.IsActive)
            .OrderBy(x => x.LineNo)
            .ThenBy(x => x.FormulaMaterialSnapshotId)
            .Select(x => new ManufacturingVUFormulaMaterialDto
            {
                FormulaMaterialSnapshotId = x.FormulaMaterialSnapshotId,
                LineNo = x.LineNo,
                CategoryId = x.CategoryId,
                ItemType = x.itemType,
                MaterialCode = x.MaterialExternalIdSnapshot,
                MaterialName = x.MaterialNameSnapshot,
                Unit = x.Unit,
                LotNo = x.LotNo,
                Quantity = x.Quantity,
                UnitPrice = canViewPrices ? x.UnitPrice : null,
                TotalPrice = canViewPrices ? x.TotalPrice : null
            })
            .ToListAsync(cancellationToken);

        var categoryIds = materialRows.Select(x => x.CategoryId).Distinct().ToArray();
        var categoryNames = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && categoryIds.Contains(x.CategoryId))
            .ToDictionaryAsync(
                x => x.CategoryId,
                x => x.Name,
                cancellationToken);

        foreach (var material in materialRows)
        {
            categoryNames.TryGetValue(material.CategoryId, out var categoryName);
            material.CategoryName = categoryName;
        }

        detail.Materials = materialRows;
        return OperationResult<ManufacturingVUFormulaDetailDto>.Ok(detail);
    }
}
