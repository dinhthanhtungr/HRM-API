using System.Text.Json.Serialization;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons
{
    public class FormulaMaterialInformationDto
    {
        public Guid? FormulaMaterialId { get; set; }
        public int LineNo { get; set; }

        public Guid ItemId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemType ItemType { get; set; }

        public Guid? CategoryId { get; set; }

        public decimal Quantity { get; set; }
        public LatestPriceSource Price { get; set; } = new LatestPriceSource();
        public decimal PriceTotal { get; set; }

        public bool HasLatestPrice { get; set; }
        public decimal? LatestUnitPrice { get; set; }
        public decimal? LatestTotalPrice { get; set; }
        public DateTime? LatestPriceDate { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LatestPriceSourceType LatestPriceSource { get; set; } = LatestPriceSourceType.Unknown;

        public IReadOnlyList<FormulaMaterialSupplierPriceDto> SupplierPrices { get; set; } = [];

        public string? ItemName { get; set; }
        public string? ItemExternalId { get; set; }
    }

    public sealed class FormulaMaterialSupplierPriceDto
    {
        public Guid MaterialsSupplierId { get; init; }
        public Guid SupplierId { get; init; }
        public string SupplierCode { get; init; } = string.Empty;
        public string SupplierName { get; init; } = string.Empty;
        public decimal? CurrentPrice { get; init; }
        public string? Currency { get; init; }
        public bool IsPreferred { get; init; }
        public DateTime? UpdatedDate { get; init; }
    }
}
