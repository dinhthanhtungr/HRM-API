using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetFormulaDrivenManufacturingBomQueue;

internal sealed class GetFormulaDrivenManufacturingBomQueueQueryHandler
    : IRequestHandler<GetFormulaDrivenManufacturingBomQueueQuery, IReadOnlyList<FormulaDrivenManufacturingBomQueueItemDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetFormulaDrivenManufacturingBomQueueQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<FormulaDrivenManufacturingBomQueueItemDto>> Handle(
        GetFormulaDrivenManufacturingBomQueueQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return [];
        }

        var formulas = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.IsSelect &&
                x.Status == FormulaStatus.Completed.ToString() &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId)
            .OrderBy(x => x.ExternalId)
            .Select(x => new FormulaRow(x.FormulaId, x.ExternalId, x.Name, x.ProductId))
            .ToListAsync(cancellationToken);
        if (formulas.Count == 0)
        {
            return [];
        }

        var versions = await _dbContext.BomVersions
            .AsNoTracking()
            .Where(x =>
                x.ChangeReason != null &&
                x.ChangeReason.StartsWith("formula-driven:") &&
                x.BomDefinition.CompanyId == companyId &&
                x.BomDefinition.BomType == BomType.Manufacturing)
            .Select(x => new BomRow(
                x.ChangeReason!,
                x.BomDefinitionId,
                x.BomVersionId,
                x.VersionNo,
                x.Status,
                x.ManufacturingStages.Any(),
                x.Items.All(item => item.ManufacturingBomStageId.HasValue)))
            .ToListAsync(cancellationToken);
        var selectedFormulaIds = formulas.Select(x => x.FormulaId).ToHashSet();
        var versionByFormula = versions
            .Select(x => new { FormulaId = FormulaDrivenManufacturingBomMarker.TryGetFormulaId(x.ChangeReason), Version = x })
            .Where(x => x.FormulaId.HasValue && selectedFormulaIds.Contains(x.FormulaId.Value))
            .GroupBy(x => x.FormulaId!.Value)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(v => v.Version.VersionNo).First().Version);

        return formulas.Select(formula =>
        {
            if (!versionByFormula.TryGetValue(formula.FormulaId, out var version))
            {
                return new FormulaDrivenManufacturingBomQueueItemDto
                {
                    FormulaId = formula.FormulaId,
                    FormulaExternalId = formula.ExternalId,
                    FormulaName = formula.Name,
                    ProductId = formula.ProductId,
                    NeedsProcessConfiguration = true
                };
            }

            return new FormulaDrivenManufacturingBomQueueItemDto
            {
                FormulaId = formula.FormulaId,
                FormulaExternalId = formula.ExternalId,
                FormulaName = formula.Name,
                ProductId = formula.ProductId,
                BomDefinitionId = version.BomDefinitionId,
                BomVersionId = version.BomVersionId,
                VersionNo = version.VersionNo,
                Status = version.Status,
                NeedsProcessConfiguration = version.Status == BomVersionStatus.Draft &&
                    (!version.HasStages || !version.AllItemsAssigned)
            };
        }).ToList();
    }

    private sealed record FormulaRow(Guid FormulaId, string ExternalId, string Name, Guid ProductId);
    private sealed record BomRow(
        string ChangeReason,
        Guid BomDefinitionId,
        Guid BomVersionId,
        int VersionNo,
        BomVersionStatus Status,
        bool HasStages,
        bool AllItemsAssigned);
}
