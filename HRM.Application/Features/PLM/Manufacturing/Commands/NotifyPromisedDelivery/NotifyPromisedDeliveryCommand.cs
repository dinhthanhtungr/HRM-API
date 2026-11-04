using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.NotifyPromisedDelivery;

/// <summary>Thông báo ngày hứa giao cho sale/leader của các ĐHG liên kết, không cập nhật lịch sản xuất.</summary>
public sealed record NotifyPromisedDeliveryCommand : IRequest<OperationResult<Guid>>
{
    [Required, StringLength(200)]
    public string ExternalId { get; init; } = string.Empty;
    public DateOnly PromisedDeliveryDate { get; init; }
}
