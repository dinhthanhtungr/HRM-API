using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulaPrefill;

/// <summary>
/// Tải Formula, vật tư hiện hành và dữ liệu yêu cầu phối mẫu gần nhất để điền form tạo lệnh.
/// </summary>
internal sealed class GetManufacturingVUFormulaPrefillQueryHandler
    : IRequestHandler<
        GetManufacturingVUFormulaPrefillQuery,
        OperationResult<ManufacturingVUFormulaPrefillDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingVUFormulaPrefillQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingVUFormulaPrefillDto>> Handle(
        GetManufacturingVUFormulaPrefillQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FormulaId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaPrefillDto>
                .Fail("FormulaId is invalid.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaPrefillDto>
                .Fail("Current company is invalid.");
        }

        var formula = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == request.FormulaId &&
                x.IsActive &&
                x.CompanyId == companyId &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId)
            .Select(x => new ManufacturingVUFormulaPrefillDto
            {
                FormulaId = x.FormulaId,
                FormulaExternalId = x.ExternalId,
                FormulaName = x.Name,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                ColourCode = x.Product.ColourCode,
                LabNote = x.Product.LabComment,
                Requirement = x.Product.Requirement,
                UsageRate = x.Product.UsageRate
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (formula is null)
        {
            return OperationResult<ManufacturingVUFormulaPrefillDto>
                .Fail("Formula was not found or is outside your company.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                x.ProductId == formula.ProductId &&
                (x.FormulaId == request.FormulaId ||
                 x.SampleRequestSampleTrials.Any(trial =>
                     trial.IsActive && trial.FormulaId == request.FormulaId)))
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.SampleRequestId)
            .Select(x => new
            {
                x.SampleRequestId,
                x.ExternalId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is not null)
        {
            formula.SampleRequestId = sampleRequest.SampleRequestId;
            formula.SampleRequestExternalId = sampleRequest.ExternalId;
        }

        var materials = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId && x.IsActive)
            .OrderBy(x => x.LineNo)
            .ThenBy(x => x.FormulaMaterialId)
            .Select(x => new ManufacturingVUFormulaPrefillMaterialDto
            {
                FormulaMaterialId = x.FormulaMaterialId,
                LineNo = x.LineNo,
                ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
                ItemType = x.itemType,
                CategoryId = x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : null,
                MaterialCode = x.MaterialExternalIdSnapshot,
                MaterialName = x.MaterialNameSnapshot,
                Unit = x.Unit,
                Quantity = x.Quantity
            })
            .ToListAsync(cancellationToken);

        var currentItemData = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            _dbContext,
            companyId,
            materials.Select(x => new FormulaItemDisplaySource(
                x.ItemId,
                x.ItemType,
                x.MaterialName,
                x.MaterialCode)),
            cancellationToken);

        foreach (var material in materials)
        {
            var display = FormulaItemDisplayResolver.Resolve(
                new FormulaItemDisplaySource(
                    material.ItemId,
                    material.ItemType,
                    material.MaterialName,
                    material.MaterialCode),
                currentItemData);
            material.MaterialName = display.Name;
            material.MaterialCode = display.ExternalId;
        }

        formula.Materials = materials;
        return OperationResult<ManufacturingVUFormulaPrefillDto>.Ok(formula);
    }
}
