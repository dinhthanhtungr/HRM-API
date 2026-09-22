using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.PLM.Formulas.Dtos.Comparison;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.CompareFormulas;

internal sealed class CompareFormulasQueryHandler(
    IPLMReadDbContext dbContext,
    IMaterialPriceQueryService materialPriceQueryService,
    IPLMFieldVisibilityService fieldVisibility,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CompareFormulasQuery, OperationResult<FormulaComparisonDto>>
{
    public async Task<OperationResult<FormulaComparisonDto>> Handle(
        CompareFormulasQuery request,
        CancellationToken cancellationToken)
    {
        if (request.BaseFormulaId == Guid.Empty || request.ComparedFormulaId == Guid.Empty)
        {
            return OperationResult<FormulaComparisonDto>.Fail(
                "BaseFormulaId and ComparedFormulaId are required.");
        }

        if (request.BaseFormulaId == request.ComparedFormulaId)
        {
            return OperationResult<FormulaComparisonDto>.Fail(
                "Base formula and compared formula must be different.");
        }

        if (currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<FormulaComparisonDto>.Fail("Current company is invalid.");
        }

        if (!fieldVisibility.CanViewFormulaMaterials() || !fieldVisibility.CanViewFormulaPrices())
        {
            return OperationResult<FormulaComparisonDto>.Fail(
                "Current user cannot view formula materials and prices.");
        }

        var currency = NormalizeCurrency(request.Currency);
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
        {
            return OperationResult<FormulaComparisonDto>.Fail(
                "Currency must be a three-letter ISO currency code.");
        }

        var formulaIds = new[] { request.BaseFormulaId, request.ComparedFormulaId };
        var formulas = await dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                formulaIds.Contains(x.FormulaId) &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive)
            .Select(x => new FormulaComparisonSource
            {
                FormulaId = x.FormulaId,
                ProductId = x.ProductId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                StepOfProduct = x.StepOfProduct
            })
            .ToListAsync(cancellationToken);

        if (formulas.Count != 2)
        {
            return OperationResult<FormulaComparisonDto>.Fail(
                "One or both formulas were not found or are not accessible.");
        }

        var materialRows = await dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                formulaIds.Contains(x.FormulaId) &&
                x.IsActive &&
                x.Formula.CompanyId == companyId &&
                x.Formula.IsActive)
            .Select(x => new FormulaComparisonMaterial
            {
                FormulaMaterialId = x.FormulaMaterialId,
                FormulaId = x.FormulaId,
                ItemId = x.MaterialId ?? x.ProductId,
                ItemType = x.itemType,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,
                Unit = x.Unit,
                ItemCode = x.MaterialExternalIdSnapshot ?? string.Empty,
                ItemName = x.MaterialNameSnapshot ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var currentItemData = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            dbContext,
            companyId,
            materialRows.Select(x => new FormulaItemDisplaySource(
                x.ItemId ?? Guid.Empty,
                x.ItemType,
                x.ItemName,
                x.ItemCode)),
            cancellationToken);

        foreach (var material in materialRows)
        {
            var display = FormulaItemDisplayResolver.Resolve(
                new FormulaItemDisplaySource(
                    material.ItemId ?? Guid.Empty,
                    material.ItemType,
                    material.ItemName,
                    material.ItemCode),
                currentItemData);
            material.ItemName = display.Name ?? string.Empty;
            material.ItemCode = display.ExternalId ?? string.Empty;
        }

        foreach (var formula in formulas)
        {
            formula.Items = materialRows
                .Where(x => x.FormulaId == formula.FormulaId)
                .ToArray();
        }

        var priceRequests = materialRows
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => new PriceItemRequest
            {
                ItemType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(x.ItemType),
                MaterialId = IsMaterial(x.ItemType) ? x.ItemId : null,
                ProductId = IsMaterial(x.ItemType) ? null : x.ItemId
            })
            .ToArray();
        var latestPriceByItem = await materialPriceQueryService
            .LoadLatestPricingItemPriceInfoDictAsync(
                companyId,
                currency,
                priceRequests,
                cancellationToken);
        var baseFormula = formulas.Single(x => x.FormulaId == request.BaseFormulaId);
        var comparedFormula = formulas.Single(x => x.FormulaId == request.ComparedFormulaId);

        return OperationResult<FormulaComparisonDto>.Ok(
            FormulaComparisonCalculator.Compare(
                baseFormula,
                comparedFormula,
                currency,
                latestPriceByItem,
                dateTimeProvider.Now));
    }

    private static bool IsMaterial(ItemType itemType)
        => itemType is ItemType.Material or ItemType.MaterialFailure;

    private static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency)
            ? "VND"
            : currency.Trim().ToUpperInvariant();
}
