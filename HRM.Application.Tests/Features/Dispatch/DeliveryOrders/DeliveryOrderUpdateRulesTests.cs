using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.UpdateDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderUpdateRulesTests
{
    [Fact]
    public void HasSameContent_ReturnsTrueForEquivalentPayload()
    {
        var merchandiseDetailId = Guid.NewGuid();
        var delivererId = Guid.NewGuid();
        var request = CreateRequest(merchandiseDetailId, delivererId, 4m, 6m);
        var deliveryOrder = CreateDeliveryOrder(merchandiseDetailId, delivererId, 4m, 6m);

        Assert.True(DeliveryOrderLotNormalizer.TryNormalize(
            request.Lines,
            out var lines,
            out _));

        Assert.True(DeliveryOrderUpdateRules.HasSameContent(
            deliveryOrder,
            request,
            lines,
            request.DelivererInforIds));
    }

    [Fact]
    public void HasSameContent_ReturnsFalseWhenLotQuantityChanges()
    {
        var merchandiseDetailId = Guid.NewGuid();
        var delivererId = Guid.NewGuid();
        var request = CreateRequest(merchandiseDetailId, delivererId, 5m, 5m);
        var deliveryOrder = CreateDeliveryOrder(merchandiseDetailId, delivererId, 4m, 6m);

        Assert.True(DeliveryOrderLotNormalizer.TryNormalize(
            request.Lines,
            out var lines,
            out _));

        Assert.False(DeliveryOrderUpdateRules.HasSameContent(
            deliveryOrder,
            request,
            lines,
            request.DelivererInforIds));
    }

    private static UpdateDeliveryOrderCommand CreateRequest(
        Guid merchandiseDetailId,
        Guid delivererId,
        decimal lotAQuantity,
        decimal lotBQuantity)
        => new()
        {
            Id = Guid.NewGuid(),
            Receiver = "Receiver",
            DeliveryAddress = "Address",
            DelivererInforIds = [delivererId],
            Lines =
            [
                new DeliveryOrderLineRequest
                {
                    MerchandiseOrderDetailId = merchandiseDetailId,
                    Quantity = lotAQuantity + lotBQuantity,
                    NumOfBags = 2,
                    Lots =
                    [
                        new DeliveryOrderLotRequest { LotNo = "LOT-A", Quantity = lotAQuantity },
                        new DeliveryOrderLotRequest { LotNo = "LOT-B", Quantity = lotBQuantity }
                    ]
                }
            ]
        };

    private static DeliveryOrder CreateDeliveryOrder(
        Guid merchandiseDetailId,
        Guid delivererId,
        decimal lotAQuantity,
        decimal lotBQuantity)
    {
        var detail = new DeliveryOrderDetail
        {
            Id = Guid.NewGuid(),
            MerchandiseOrderDetailId = merchandiseDetailId,
            Quantity = lotAQuantity + lotBQuantity,
            NumOfBags = 2,
            LotNoList = "LOT-A, LOT-B",
            IsActive = true,
            IsAttach = false,
            LotConsumptions =
            [
                new DeliveryOrderDetailLotConsumption
                {
                    LotNo = "LOT-A",
                    Quantity = lotAQuantity,
                    IsActive = true
                },
                new DeliveryOrderDetailLotConsumption
                {
                    LotNo = "LOT-B",
                    Quantity = lotBQuantity,
                    IsActive = true
                }
            ]
        };

        return new DeliveryOrder
        {
            Id = Guid.NewGuid(),
            Receiver = "Receiver",
            DeliveryAddress = "Address",
            Details = [detail],
            Deliverers =
            [
                new Deliverer
                {
                    DelivererInforId = delivererId
                }
            ]
        };
    }
}
