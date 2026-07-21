using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaById;

internal sealed class GetFormulaByIdQueryHandler
    : IRequestHandler<GetFormulaByIdQuery, FormulaInformationDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;

    public GetFormulaByIdQueryHandler(
        IPLMReadDbContext dbContext,
        IPLMFieldVisibilityService fieldVisibility,
        IMaterialPriceQueryService materialPriceQueryService)
    {
        _dbContext = dbContext;
        _fieldVisibility = fieldVisibility;
        _materialPriceQueryService = materialPriceQueryService;
    }

    public async Task<FormulaInformationDto?> Handle(
        GetFormulaByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FormulaId == Guid.Empty)
        {
            return null;
        }

        var canViewFormulaPrices = _fieldVisibility.CanViewFormulaPrices();

        var items = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId && x.IsActive)
            .Select(x => new FormulaMaterialInformationDto
            {
                FormulaMaterialId = x.FormulaMaterialId,
                LineNo = x.LineNo,

                ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
                ItemType = x.itemType,

                CategoryId = x.CategoryId,

                Quantity = x.Quantity,
                Price = new LatestPriceSource(),
                PriceTotal = 0m,

                ItemName = (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure)
                    ? (x.Material != null ? x.Material.Name : x.MaterialNameSnapshot)
                    : (x.Product != null ? x.Product.Name : x.MaterialNameSnapshot),

                ItemExternalId = (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure)
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

        if (canViewFormulaPrices)
        {
            var priceRequests = items
                .Where(x => x.ItemId != Guid.Empty)
                .Select(x => new PriceItemRequest
                {
                    ItemType = NormalizePriceItemType(x.ItemType),
                    MaterialId = x.ItemType == ItemType.Material || x.ItemType == ItemType.MaterialFailure
                        ? x.ItemId
                        : null,
                    ProductId = x.ItemType == ItemType.Product || x.ItemType == ItemType.ProductFailure
                        ? x.ItemId
                        : null
                })
                .ToList();

            var latestPriceDict = await _materialPriceQueryService
                .LoadLatestItemPriceInfoDictAsync(priceRequests, cancellationToken);

            foreach (var item in items)
            {
                var normalizedItemType = NormalizePriceItemType(item.ItemType);

                var latestUnitPrice = MaterialPriceResolver.ResolveLatestItemPrice(
                    latestPriceDict,
                    normalizedItemType,
                    item.ItemId,
                    0m);

                var latestPriceSource = MaterialPriceResolver.ResolveLatestItemPriceSource(
                    latestPriceDict,
                    normalizedItemType,
                    item.ItemId);

                item.Price = new LatestPriceSource
                {
                    UnitPrice = latestUnitPrice,
                    LatestPriceDate = MaterialPriceResolver.ResolveLatestItemPriceDate(
                        latestPriceDict,
                        normalizedItemType,
                        item.ItemId),
                    Source = latestPriceSource
                };
                item.PriceTotal = item.Quantity * latestUnitPrice;
            }
        }
        else
        {
            foreach (var item in items)
            {
                item.Price = new LatestPriceSource();
                item.PriceTotal = 0m;
            }
        }

        return await _dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId && x.IsActive)
            .Select(x => new FormulaInformationDto
            {
                FormulaId = x.FormulaId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,

                CheckBy = x.CheckBy,
                CheckByName = x.CheckByNavigation != null
                    ? x.CheckByNavigation.FullName
                    : string.Empty,
                CheckDate = x.CheckDate,

                SentBy = x.SentBy,
                SentByName = x.SentByNavigation != null
                    ? x.SentByNavigation.FullName
                    : string.Empty,
                SentDate = x.SentDate,

                TotalPrice = canViewFormulaPrices ? x.TotalPrice : null,
                EffectiveDate = x.EffectiveDate,
                ProductionPrice = canViewFormulaPrices ? x.ProductionPrice : null,
                PresidentPrice = canViewFormulaPrices ? x.PresidentPrice : null,
                ProfitMarginPrice = canViewFormulaPrices ? x.ProfitMarginPrice : null,

                IsSelect = x.IsSelect,
                IsActive = x.IsActive,
                Note = x.Note ?? string.Empty,
                CreatedDate = x.CreatedDate,

                Materials = items
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ItemType NormalizePriceItemType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.MaterialFailure => ItemType.Material,
            ItemType.ProductFailure => ItemType.Product,
            _ => itemType
        };
    }
}
