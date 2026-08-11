using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.ExportManufacturingVUFormulaPdf;

internal sealed class ExportManufacturingVUFormulaPdfQueryHandler
    : IRequestHandler<
        ExportManufacturingVUFormulaPdfQuery,
        OperationResult<ManufacturingVUFormulaPdfFileDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IWarehouseReadDbContext _warehouseDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IManufacturingVUFormulaPdfRenderer _renderer;

    public ExportManufacturingVUFormulaPdfQueryHandler(
        IPLMReadDbContext dbContext,
        IWarehouseReadDbContext warehouseDbContext,
        ICurrentUser currentUser,
        IManufacturingVUFormulaPdfRenderer renderer)
    {
        _dbContext = dbContext;
        _warehouseDbContext = warehouseDbContext;
        _currentUser = currentUser;
        _renderer = renderer;
    }

    public async Task<OperationResult<ManufacturingVUFormulaPdfFileDto>> Handle(
        ExportManufacturingVUFormulaPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ManufacturingVUFormulaId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaPdfFileDto>
                .Fail("ManufacturingVUFormulaId is invalid.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaPdfFileDto>
                .Fail("Current company is invalid.");
        }

        var document = await _dbContext.ManufacturingVUFormulas
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingVUFormulaId == request.ManufacturingVUFormulaId &&
                x.Formula.Product.CompanyId == companyId)
            .Select(x => new ManufacturingVUFormulaPdfDocumentDto
            {
                ProductId = x.Formula.ProductId,
                FormulaExternalId = x.Formula.ExternalId,
                FormulaName = x.Formula.Name,
                ProductName = x.Formula.Product.Name,
                ColourCode = x.Formula.Product.ColourCode,
                UsageRate = x.Formula.Product.UsageRate,
                TotalProductionQuantity = x.TotalProductionQuantity ?? 0m,
                NumOfBatches = x.NumOfBatches ?? 0,
                LabNote = x.LabNote,
                Requirement = x.Requirement,
                QcCheck = x.QcCheck
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return OperationResult<ManufacturingVUFormulaPdfFileDto>
                .Fail("Sample production order was not found or is outside your company.");
        }

        var customer = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                x.ProductId == document.ProductId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                CustomerCode = x.Customer.ExternalId,
                x.RequestDeliveryDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        document.CustomerCode = customer?.CustomerCode;
        document.RequestDate = customer?.RequestDeliveryDate;

        var materials = await _dbContext.FormulaMaterialSnapshots
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingVUFormulaId == request.ManufacturingVUFormulaId &&
                x.IsActive &&
                x.Quantity > 0)
            .OrderBy(x => x.LineNo)
            .ThenBy(x => x.FormulaMaterialSnapshotId)
            .Select(x => new
            {
                x.CategoryId,
                x.LineNo,
                x.MaterialExternalIdSnapshot,
                x.MaterialNameSnapshot,
                x.LotNo,
                x.Quantity
            })
            .ToListAsync(cancellationToken);

        var categoryIds = materials.Select(x => x.CategoryId).Distinct().ToArray();
        var categoryNames = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && categoryIds.Contains(x.CategoryId))
            .ToDictionaryAsync(
                x => x.CategoryId,
                x => x.Name,
                cancellationToken);

        var missingLotCodes = materials
            .Where(x => string.IsNullOrWhiteSpace(x.LotNo))
            .Select(x => x.MaterialExternalIdSnapshot?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var stockRows = missingLotCodes.Length == 0
            ? []
            : await _warehouseDbContext.WarehouseShelfStocks
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    missingLotCodes.Contains(x.Code) &&
                    x.WarehouseShelves != null &&
                    x.WarehouseShelves.IsActive &&
                    x.StockType == StockType.RawMaterial &&
                    x.LotNo != null &&
                    x.LotNo != string.Empty &&
                    x.QtyKg > 0)
                .Select(x => new
                {
                    x.Code,
                    x.LotNo,
                    x.UpdatedDate,
                    x.ShelfStockId
                })
                .ToListAsync(cancellationToken);

        var currentLotByCode = stockRows
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.UpdatedDate)
                    .ThenByDescending(x => x.ShelfStockId)
                    .Select(x => x.LotNo!.Trim())
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        document.Materials = materials.Select(x =>
        {
            categoryNames.TryGetValue(x.CategoryId, out var categoryName);
            var materialCode = x.MaterialExternalIdSnapshot?.Trim() ?? string.Empty;
            var lotNo = x.LotNo;
            if (string.IsNullOrWhiteSpace(lotNo) &&
                currentLotByCode.TryGetValue(materialCode, out var currentLotNo))
            {
                lotNo = currentLotNo;
            }

            return new ManufacturingVUFormulaPdfMaterialDto
            {
                LineNo = x.LineNo,
                CategoryName = categoryName,
                MaterialCode = materialCode,
                MaterialName = x.MaterialNameSnapshot ?? string.Empty,
                LotNo = lotNo,
                Quantity = x.Quantity
            };
        }).ToList();

        var content = _renderer.Render(document);
        return OperationResult<ManufacturingVUFormulaPdfFileDto>.Ok(
            new ManufacturingVUFormulaPdfFileDto
            {
                FileName = $"Lenh-san-xuat-mau-{SanitizeFileName(document.FormulaExternalId)}.pdf",
                Content = content
            });
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "VU" : sanitized;
    }
}
