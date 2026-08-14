using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaVersions;

internal sealed class GetFormulaVersionsQueryHandler
    : IRequestHandler<GetFormulaVersionsQuery, IReadOnlyList<FormulaVersionDto>?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPLMFieldVisibilityService _fieldVisibility;

    public GetFormulaVersionsQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IPLMFieldVisibilityService fieldVisibility)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fieldVisibility = fieldVisibility;
    }

    public async Task<IReadOnlyList<FormulaVersionDto>?> Handle(
        GetFormulaVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FormulaId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return null;
        }

        var formulaExists = await _dbContext.Formulas
            .AsNoTracking()
            .AnyAsync(x =>
                x.FormulaId == request.FormulaId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);

        if (!formulaExists)
        {
            return null;
        }

        var canViewPrices = _fieldVisibility.CanViewFormulaPrices();
        var canViewMaterials = _fieldVisibility.CanViewFormulaMaterials();

        return await _dbContext.FormulaVersions
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new FormulaVersionDto
            {
                FormulaVersionId = x.FormulaVersionId,
                FormulaId = x.FormulaId,
                VersionNo = x.VersionNo,
                Name = x.Name,
                Status = x.Status,
                Note = x.Note,
                TotalPrice = canViewPrices ? x.TotalPrice : null,
                ProductionPrice = canViewPrices ? x.ProductionPrice : null,
                PresidentPrice = canViewPrices ? x.PresidentPrice : null,
                ProfitMarginPrice = canViewPrices ? x.ProfitMarginPrice : null,
                EffectiveFrom = x.EffectiveFrom,
                EffectiveTo = x.EffectiveTo,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
                ChangeReason = x.ChangeReason,
                Items = canViewMaterials
                    ? x.Items.OrderBy(item => item.LineNo).Select(item => new FormulaVersionItemDto
                    {
                        FormulaVersionItemId = item.FormulaVersionItemId,
                        LineNo = item.LineNo,
                        ItemType = item.ItemType,
                        MaterialId = item.MaterialId,
                        ProductId = item.ProductId,
                        CategoryId = item.CategoryId,
                        Quantity = item.Quantity,
                        UnitPrice = canViewPrices ? item.UnitPrice : null,
                        TotalPrice = canViewPrices ? item.TotalPrice : null,
                        Unit = item.Unit,
                        MaterialExternalIdSnapshot = item.MaterialExternalIdSnapshot,
                        MaterialNameSnapshot = item.MaterialNameSnapshot
                    }).ToList()
                    : null
            })
            .ToListAsync(cancellationToken);
    }
}
