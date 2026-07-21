using System.Text.Json.Serialization;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons
{
    public class LatestPriceSource
    {
        public DateTime? LatestPriceDate { get; set; }
        public decimal? UnitPrice { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LatestPriceSourceType Source { get; set; } = LatestPriceSourceType.Unknown;
    }
}
