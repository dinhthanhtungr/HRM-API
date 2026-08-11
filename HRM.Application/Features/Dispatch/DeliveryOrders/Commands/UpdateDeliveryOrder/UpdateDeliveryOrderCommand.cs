using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.UpdateDeliveryOrder
{
    public sealed class UpdateDeliveryOrderCommand : IRequest<OperationResult<Guid>>
    {
        public Guid Id { get; init; }
        public string? Receiver { get; init; }
        public string? DeliveryAddress { get; init; }
        public string? PaymentType { get; init; }
        public string? PaymentDeadline { get; init; }
        public string? TaxNumber { get; init; }
        public string? PhoneSnapshot { get; init; }
        public string? Note { get; init; }
        public decimal? DeliveryPrice { get; init; }
        public bool? RequiresUnloading { get; init; }

        public IReadOnlyCollection<Guid> DelivererInforIds { get; init; } = [];
        public IReadOnlyCollection<DeliveryOrderLineRequest> Lines { get; init; } = [];
    }
}
