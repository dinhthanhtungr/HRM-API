using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.ExportProductInspectionPdf;

internal sealed class ExportProductInspectionPdfQueryHandler
    : IRequestHandler<ExportProductInspectionPdfQuery, OperationResult<ProductInspectionPdfFileDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IProductInspectionPdfRenderer _renderer;

    public ExportProductInspectionPdfQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IProductInspectionPdfRenderer renderer)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _renderer = renderer;
    }

    public async Task<OperationResult<ProductInspectionPdfFileDto>> Handle(
        ExportProductInspectionPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return OperationResult<ProductInspectionPdfFileDto>.Fail("Id is invalid.");

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<ProductInspectionPdfFileDto>.Fail("Current company is invalid.");

        var inspection = await _dbContext.ProductInspections
            .AsNoTracking()
            .Where(x => x.Id == request.Id && x.ProductStandardId.HasValue &&
                _dbContext.ProductStandards.Any(s =>
                    s.Id == x.ProductStandardId.Value && s.CompanyId == companyId))
            .FirstOrDefaultAsync(cancellationToken);

        if (inspection is null)
            return OperationResult<ProductInspectionPdfFileDto>.Fail("Product inspection was not found or is outside your company.");

        var standard = await _dbContext.ProductStandards
            .AsNoTracking()
            .Where(x => x.Id == inspection.ProductStandardId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new
            {
                ExpiryType = x.Product!.ExpiryType,
                Specifications = new ProductInspectionPdfSpecificationsDto
                {
                    Shape = x.Shape,
                    PelletSize = x.PelletSize,
                    DeltaE = x.DeltaE,
                    Moisture = x.Moisture,
                    Density = x.Density,
                    MeltIndex = x.MeltIndex,
                    TensileStrength = x.TensileStrength,
                    ElongationAtBreak = x.ElongationAtBreak,
                    FlexuralStrength = x.FlexuralStrength,
                    FlexuralModulus = x.FlexuralModulus,
                    IzodImpactStrength = x.IzodImpactStrength,
                    Hardness = x.Hardness,
                    DwellTime = x.DwellTime,
                    BlackDots = x.BlackDots,
                    MigrationTest = x.MigrationTest
                }
            })
            .FirstOrDefaultAsync(cancellationToken);

        var batchCandidates = BuildBatchCandidates(inspection.BatchId);
        var bagType = batchCandidates.Length == 0
            ? null
            : await _dbContext.ProductTests
                .AsNoTracking()
                .Where(x => x.ExternalId != null && batchCandidates.Contains(x.ExternalId))
                .Select(x => x.ProductPackage)
                .FirstOrDefaultAsync(cancellationToken);

        var document = ProductInspectionDtoMapper.ToPdfDocument(
            inspection,
            standard?.ExpiryType,
            bagType,
            standard?.Specifications);
        var content = _renderer.Render(document, request.TemplateOnly);
        if (content.Length == 0)
            return OperationResult<ProductInspectionPdfFileDto>.Fail("Generated PDF is empty.");

        var fileCode = SanitizeFileName(inspection.ExternalId ?? inspection.Id.ToString());
        return OperationResult<ProductInspectionPdfFileDto>.Ok(new ProductInspectionPdfFileDto
        {
            FileName = $"COA-{fileCode}.pdf",
            Content = content
        });
    }

    private static string[] BuildBatchCandidates(string? batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId)) return [];
        var value = batchId.Trim();
        var alternate = value.StartsWith("VA", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : $"VA{value}";
        return [value, alternate];
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
    }
}
