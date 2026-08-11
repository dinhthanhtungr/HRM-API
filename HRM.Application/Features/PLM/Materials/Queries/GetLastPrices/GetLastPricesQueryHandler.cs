using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Materials.Dtos.GetLastPrices;
using HRM.Domain.Enums.Formulas;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetLastPrices
{
    internal sealed class GetLastPricesQueryHandler
        : IRequestHandler<GetLastPricesQuery, GetLastPricesResultDto>
    {
        private readonly IMaterialPriceQueryService _priceQueryService;

        public GetLastPricesQueryHandler(IMaterialPriceQueryService priceQueryService)
        {
            _priceQueryService = priceQueryService;
        }

        public async Task<GetLastPricesResultDto> Handle(
            GetLastPricesQuery request,
            CancellationToken cancellationToken)
        {
            var materialPrices = request.SupplierId is { } supplierId && supplierId != Guid.Empty
                ? await _priceQueryService.LoadLatestMaterialPriceInfoBySupplierDictAsync(
                    supplierId,
                    request.MaterialIds,
                    cancellationToken)
                : await _priceQueryService.LoadLatestMaterialPriceInfoDictAsync(
                    request.MaterialIds,
                    cancellationToken);

            var itemPrices = await LoadItemPricesAsync(request, cancellationToken);

            return new GetLastPricesResultDto
            {
                Materials = materialPrices
                    .Values
                    .Select(GetLastMaterialPriceItemDto.FromPrice)
                    .OrderBy(x => x.MaterialId)
                    .ToList(),
                Items = itemPrices
                    .Values
                    .Select(GetLastItemPriceItemDto.FromPrice)
                    .OrderBy(x => x.ItemType)
                    .ThenBy(x => x.ItemId)
                    .ToList()
            };
        }

        private Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadItemPricesAsync(
            GetLastPricesQuery request,
            CancellationToken cancellationToken)
        {
            var items = request.Items
                .Where(x =>
                    (x.ItemType == ItemType.Material && 
                     x.MaterialId.HasValue &&
                     x.MaterialId.Value != Guid.Empty) ||
                    (x.ItemType == ItemType.Product &&
                     x.ProductId.HasValue &&
                     x.ProductId.Value != Guid.Empty))
                .ToList();

            return items.Count == 0
                ? Task.FromResult(new Dictionary<PriceItemKey, LatestItemPriceDto>())
                : _priceQueryService.LoadLatestItemPriceInfoDictAsync(items, cancellationToken);
        }
    }
}
