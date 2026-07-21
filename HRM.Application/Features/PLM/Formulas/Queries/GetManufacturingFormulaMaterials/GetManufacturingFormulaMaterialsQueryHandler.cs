using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetManufacturingFormulaMaterials;

internal sealed class GetManufacturingFormulaMaterialsQueryHandler
    : IRequestHandler<GetManufacturingFormulaMaterialsQuery, FormulaDto>
{
    private readonly IPLMReadDbContext _dbContext;

    public GetManufacturingFormulaMaterialsQueryHandler(IPLMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FormulaDto> Handle(
        GetManufacturingFormulaMaterialsQuery request,
        CancellationToken cancellationToken)
    {
        var formula = await _dbContext.ManufacturingFormulas
            .AsNoTracking()
            .Where(x => x.ManufacturingFormulaId == request.ManufacturingFormulaId && x.IsActive)
            .Select(x => new FormulaDto
            {
                FormulaId = x.ManufacturingFormulaId,
                ExternalId = x.ExternalId,
                Note = x.Note ?? string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (formula is null)
        {
            return new FormulaDto
            {
                FormulaId = request.ManufacturingFormulaId,
                Items = new List<FormulaMaterialDto>()
            };
        }

        var items = await _dbContext.ManufacturingFormulaMaterials
            .AsNoTracking()
            .Where(x => x.IsActive == true && x.ManufacturingFormulaId == request.ManufacturingFormulaId)
            .OrderBy(x => x.LineNo)
            .Select(x => new FormulaMaterialDto
            {
                FormulaMaterialId = x.ManufacturingFormulaMaterialId,
                LineNo = x.LineNo,
                ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
                ItemType = x.itemType,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,
                MaterialNameSnapshot = (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure)
                    ? (x.Material != null ? x.Material.Name : x.MaterialNameSnapshot)
                    : (x.Product != null ? x.Product.Name : x.MaterialNameSnapshot),
                MaterialExternalIdSnapshot = (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure)
                    ? (x.Material != null ? x.Material.ExternalId : x.MaterialExternalIdSnapshot)
                    : (x.Product != null
                        ? x.Product.SampleRequests
                            .Where(sr => sr.IsActive)
                            .OrderByDescending(sr => sr.CreatedDate)
                            .Select(sr => sr.ExternalId)
                            .FirstOrDefault()
                        : x.MaterialExternalIdSnapshot)
            })
            .ToListAsync(cancellationToken);

        formula.Items = items;

        return formula;
    }
}
