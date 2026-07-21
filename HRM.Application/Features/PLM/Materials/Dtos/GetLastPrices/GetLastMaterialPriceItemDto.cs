using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;

namespace HRM.Application.Features.PLM.Materials.Dtos.GetLastPrices
{
    public sealed class GetLastMaterialPriceItemDto
    {
        public Guid MaterialId { get; init; }
        public decimal CurrentPrice { get; init; }
        public DateTime? PriceDate { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MaterialPriceSource PriceSource { get; init; } = MaterialPriceSource.Unknown;

        public static GetLastMaterialPriceItemDto FromPrice(LatestMaterialPriceDto price)
        {
            return new GetLastMaterialPriceItemDto
            {
                MaterialId = price.MaterialId,
                CurrentPrice = price.CurrentPrice,
                PriceDate = price.PriceDate,
                PriceSource = price.PriceSource
            };
        }
    }
}
