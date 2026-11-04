using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Manufacturings;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;

public sealed record ApplyProductionOrderActionCommand(
    Guid Id,
    ManufacturingInformationAction Action,
    ManufacturingInformationActionRequest Request
) : IRequest<OperationResult>;
