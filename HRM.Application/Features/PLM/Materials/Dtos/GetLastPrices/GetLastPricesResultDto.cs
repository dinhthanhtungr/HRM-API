using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Materials.Dtos.GetLastPrices
{
    public sealed class GetLastPricesResultDto
    {
        public IReadOnlyList<GetLastMaterialPriceItemDto> Materials { get; init; } = [];
        public IReadOnlyList<GetLastItemPriceItemDto> Items { get; init; } = [];
    }
}
