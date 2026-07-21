using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Materials.Dtos.GetLastPrices
{
    public sealed class GetLastItemPriceItemDto
    {
        public ItemType ItemType { get; init; }
        public Guid ItemId { get; init; }
        public decimal CurrentPrice { get; init; }
        public DateTime? PriceDate { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LatestPriceSourceType PriceSource { get; init; } = LatestPriceSourceType.Unknown;

        public static GetLastItemPriceItemDto FromPrice(LatestItemPriceDto price)
        {
            return new GetLastItemPriceItemDto
            {
                ItemType = price.ItemType,
                ItemId = price.ItemId,
                CurrentPrice = price.CurrentPrice,
                PriceDate = price.PriceDate,
                PriceSource = price.PriceSource
            };
        }
    }
}
