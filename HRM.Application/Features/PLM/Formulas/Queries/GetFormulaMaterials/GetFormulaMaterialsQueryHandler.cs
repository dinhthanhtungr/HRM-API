using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaMaterials;

internal sealed class GetFormulaMaterialsQueryHandler
    : IRequestHandler<GetFormulaMaterialsQuery, FormulaDto>
{
    private readonly IPLMReadDbContext _dbContext;

    public GetFormulaMaterialsQueryHandler(IPLMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FormulaDto> Handle(
        GetFormulaMaterialsQuery request,
        CancellationToken cancellationToken)
    {
        var formulaHeader = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId && x.IsActive)
            .Select(x => new
            {
                FormulaId = x.FormulaId,
                ExternalId = x.ExternalId,
                Note = x.Note ?? string.Empty,
                x.CompanyId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (formulaHeader is null)
        {
            return new FormulaDto
            {
                FormulaId = request.FormulaId,
                Items = new List<FormulaMaterialDto>()
            };
        }

        var items = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => x.IsActive == true && x.FormulaId == request.FormulaId)
            .OrderBy(x => x.LineNo)
            .Select(x => new FormulaMaterialDto
            {
                FormulaMaterialId = x.FormulaMaterialId,
                LineNo = x.LineNo,
                ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
                ItemType = x.itemType,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,
                MaterialNameSnapshot = x.MaterialNameSnapshot,
                MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot
            })
            .ToListAsync(cancellationToken);

        var currentItemData = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            _dbContext,
            formulaHeader.CompanyId ?? Guid.Empty,
            items.Select(item => new FormulaItemDisplaySource(
                item.ItemId,
                item.ItemType,
                item.MaterialNameSnapshot,
                item.MaterialExternalIdSnapshot)),
            cancellationToken);

        foreach (var item in items)
        {
            var display = FormulaItemDisplayResolver.Resolve(
                new FormulaItemDisplaySource(
                    item.ItemId,
                    item.ItemType,
                    item.MaterialNameSnapshot,
                    item.MaterialExternalIdSnapshot),
                currentItemData);
            item.MaterialNameSnapshot = display.Name;
            item.MaterialExternalIdSnapshot = display.ExternalId;
        }

        return new FormulaDto
        {
            FormulaId = formulaHeader.FormulaId,
            ExternalId = formulaHeader.ExternalId,
            Note = formulaHeader.Note,
            Items = items
        };
    }
}
