using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;

public enum DeliveryReportKind { Plan, Finish, Transport }

public sealed record ExportDeliveryReportQuery(DateTime From, DateTime To, DeliveryReportKind Kind)
    : IRequest<OperationResult<DeliveryOrderFileDto>>;
