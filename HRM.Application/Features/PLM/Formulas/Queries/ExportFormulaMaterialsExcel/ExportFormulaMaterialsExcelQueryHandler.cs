using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Exports;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.ExportFormulaMaterialsExcel;

internal sealed class ExportFormulaMaterialsExcelQueryHandler
    : IRequestHandler<ExportFormulaMaterialsExcelQuery, OperationResult<FormulaMaterialsExcelExportFileDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;
    private readonly IFormulaMaterialsExcelRenderer _renderer;

    public ExportFormulaMaterialsExcelQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IPLMFieldVisibilityService fieldVisibility,
        IMaterialPriceQueryService materialPriceQueryService,
        IFormulaMaterialsExcelRenderer renderer)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fieldVisibility = fieldVisibility;
        _materialPriceQueryService = materialPriceQueryService;
        _renderer = renderer;
    }

    public async Task<OperationResult<FormulaMaterialsExcelExportFileDto>> Handle(
        ExportFormulaMaterialsExcelQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FormulaId == Guid.Empty)
        {
            return OperationResult<FormulaMaterialsExcelExportFileDto>.Fail("FormulaId is invalid.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<FormulaMaterialsExcelExportFileDto>.Fail("Current company is invalid.");
        }

        if (!_fieldVisibility.CanViewFormulaMaterials() || !_fieldVisibility.CanViewFormulaPrices())
        {
            return OperationResult<FormulaMaterialsExcelExportFileDto>.Fail(
                "You are not allowed to export formula material prices.");
        }

        var formula = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new { x.ExternalId, x.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (formula is null)
        {
            return OperationResult<FormulaMaterialsExcelExportFileDto>.Fail(
                "Formula was not found or is outside your company.");
        }

        var materialRows = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == request.FormulaId &&
                x.IsActive &&
                x.Formula.CompanyId == companyId &&
                (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure))
            .OrderBy(x => x.LineNo)
            .ThenBy(x => x.FormulaMaterialId)
            .Select(x => new
            {
                x.LineNo,
                x.MaterialId,
                x.Quantity,
                x.MaterialExternalIdSnapshot,
                x.MaterialNameSnapshot
            })
            .ToListAsync(cancellationToken);

        var latestPriceByMaterialId = await _materialPriceQueryService
            .LoadLatestMaterialPriceInfoDictAsync(
                materialRows.Select(x => x.MaterialId),
                cancellationToken);

        var document = new FormulaMaterialsExcelExportDocumentDto
        {
            FormulaExternalId = formula.ExternalId,
            FormulaName = formula.Name,
            Materials = materialRows.Select(row => new FormulaMaterialsExcelExportLineDto
            {
                LineNo = row.LineNo,
                MaterialCode = row.MaterialExternalIdSnapshot?.Trim() ?? string.Empty,
                MaterialName = row.MaterialNameSnapshot?.Trim() ?? string.Empty,
                Quantity = row.Quantity,
                LatestUnitPrice = row.MaterialId is { } materialId &&
                    latestPriceByMaterialId.TryGetValue(materialId, out var latestPrice) &&
                    latestPrice.PriceSource != MaterialPriceSource.Unknown
                    ? latestPrice.CurrentPrice
                    : null
            }).ToList()
        };

        return OperationResult<FormulaMaterialsExcelExportFileDto>.Ok(
            new FormulaMaterialsExcelExportFileDto
            {
                FileName = $"Danh-sach-NVL-{SanitizeFileName(formula.ExternalId)}.xlsx",
                Content = _renderer.Render(document)
            });
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Formula" : sanitized;
    }
}
