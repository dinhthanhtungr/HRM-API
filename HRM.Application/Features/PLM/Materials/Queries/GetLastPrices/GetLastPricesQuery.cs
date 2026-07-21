using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Features.PLM.Materials.Dtos.GetLastPrices;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetLastPrices
{
    public sealed class GetLastPricesQuery : IRequest<GetLastPricesResultDto>
    {
        public IReadOnlyCollection<Guid?> MaterialIds { get; init; } = [];
        public Guid? SupplierId { get; init; }
        public IReadOnlyCollection<PriceItemRequest> Items { get; init; } = [];
    }
}
