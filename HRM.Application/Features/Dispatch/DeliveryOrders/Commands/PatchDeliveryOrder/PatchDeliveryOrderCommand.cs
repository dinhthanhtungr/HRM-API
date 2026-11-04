using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PatchDeliveryOrder;

public sealed class PatchDeliveryOrderCommand : IRequest<OperationResult>
{
    public Guid Id { get; set; }
    public string? Note { get; init; }
    public decimal? DeliveryPrice { get; init; }
    public List<Guid>? DelivererInforIds { get; init; }
    public List<DeliveryLotBagUpdate>? LotBags { get; init; }
    public List<DeliveryBagUpdate>? Details { get; init; }
}

public sealed record DeliveryBagUpdate(Guid Id, int NumOfBags);

public sealed record DeliveryLotBagUpdate(string ProductCode, string LotNo, string StockType, int NumOfBags);
