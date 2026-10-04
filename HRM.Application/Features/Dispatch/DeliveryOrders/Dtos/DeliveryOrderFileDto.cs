namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed record DeliveryOrderFileDto(string FileName, string ContentType, byte[] Content);
